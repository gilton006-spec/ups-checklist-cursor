"""Check synthetic exports with pypdf, MuPDF and Poppler, not raw compressed bytes.

Requires: pip install pypdf pymupdf pillow; Poppler's pdftoppm on PATH.
Usage: python dotnet/tools/validate_pdf_exports.py <export-directory>
"""

import json
import subprocess
import sys
from pathlib import Path

import fitz
from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def operations(stream, reader, resources, label):
    if stream is None:
        return
    content = ContentStream(stream, reader)
    arities = {b"rg": 3, b"RG": 3, b"g": 1, b"G": 1, b"cm": 6,
               b"Tm": 6, b"Tf": 2, b"re": 4, b"BDC": 2, b"BMC": 1}
    for args, op in content.operations:
        check(b"," not in op, f"{label}: locale-dependent operator {op!r}")
        if op in arities:
            check(len(args) == arities[op], f"{label}: invalid {op!r} operands")
        if op == b"Tf":
            fonts = resources.get("/Font", {}).get_object() if resources.get("/Font") else {}
            check(args[0] in fonts, f"{label}: undefined font {args[0]}")
        if op == b"Do":
            images = resources.get("/XObject", {}).get_object() if resources.get("/XObject") else {}
            check(args[0] in images, f"{label}: undefined XObject {args[0]}")


def validate(directory, spec):
    file = directory / spec["file"]
    reader = PdfReader(file)
    form = reader.trailer["/Root"]["/AcroForm"]
    need_appearances = form.get("/NeedAppearances", False)
    check(not getattr(need_appearances, "value", need_appearances), "NeedAppearances must be false")
    fields = reader.get_fields() or {}
    for key, value in {"name": spec["name"], "date": spec["date"],
                       "package_count": spec["count"]}.items():
        check(key in fields and fields[key].get("/V") == value, f"{key}: missing/stale canonical value")
    for name, checked in spec["checks"].items():
        expected = "/Yes" if checked else "/Off"
        check(name in fields and fields[name].get("/V") == expected, f"{name}: incorrect checkbox state")
    if not spec["drawn"]:
        check(fields["signature"].get("/V") == spec["signature"], "Signature value lost")
    remark_parts = [str(field.get("/V", "")) for name, field in fields.items()
                    if name == "remarks" or name.startswith("remarks_continued_")]
    check("".join("".join(remark_parts).split()) == "".join(spec["remarks"].split()), "Remarks truncated")

    views = fitz.open(file)
    minimum_images = sum(len(p.get_image_info()) for p in views)
    check(minimum_images >= spec["minimumImages"], "Missing drawn reference/evidence/signature images")
    target = directory / "renders" / file.stem
    target.mkdir(parents=True, exist_ok=True)
    command = subprocess.run(["pdftoppm", "-r", "96", "-png", str(file), str(target / "poppler")],
                             capture_output=True, text=True)
    check(command.returncode == 0, f"Poppler failed: {command.stderr}")
    check(not command.stderr.strip(), f"Poppler warnings: {command.stderr}")
    poppler_files = sorted(target.glob("poppler-*.png"))
    check(len(poppler_files) == len(reader.pages), "Missing rendered pages")

    for index, (page, view) in enumerate(zip(reader.pages, views)):
        label = f"page {index + 1}"
        resources = page.get("/Resources", {}).get_object()
        operations(page.get_contents(), reader, resources, label)
        check(spec["title"] in view.get_text(), f"{label}: missing/clipped heading")
        pixmap = view.get_pixmap(matrix=fitz.Matrix(96 / 72, 96 / 72), alpha=False)
        pixmap.save(target / f"mupdf-{index + 1}.png")
        mupdf_image = Image.frombytes("RGB", [pixmap.width, pixmap.height], pixmap.samples)
        with Image.open(poppler_files[index]) as poppler_image:
            for ref in page.get("/Annots", []):
                widget = ref.get_object()
                if widget.get("/Subtype") != "/Widget":
                    continue
                field = widget
                while "/T" not in field and "/Parent" in field:
                    field = field["/Parent"]
                name = str(field.get("/T", ""))
                check(name in fields, f"{label}: orphan widget {name}")
                check(widget.get("/P") == page.indirect_reference, f"{name}: wrong page association")
                appearance = widget.get("/AP", {}).get("/N")
                check(appearance is not None, f"{name}: missing normal appearance")
                appearance = appearance.get_object()
                if field.get("/FT") == "/Btn":
                    check(widget.get("/AS") == fields[name].get("/V"), f"{name}: stale visual checkbox state")
                    appearance = appearance[widget["/AS"]].get_object()
                check(bool(appearance.get_data()), f"{name}: empty appearance")
                operations(appearance, reader, appearance.get("/Resources", {}).get_object(), name)
                da = field.get("/DA", form.get("/DA"))
                if field.get("/FT") == "/Tx":
                    check(da is not None, f"{name}: no default appearance")
                    stream = DecodedStreamObject()
                    stream.set_data(str(da).encode("latin1"))
                    operations(stream, reader, form.get("/DR", {}).get_object(), name + " DA")

                # The logical value alone is not proof that a user can see it.
                value = fields[name].get("/V")
                requires_ink = value == "/Yes" if field.get("/FT") == "/Btn" else bool(value)
                if requires_ink:
                    x0, y0, x1, y1 = map(float, widget["/Rect"])
                    height = float(page.mediabox.height)
                    for renderer, image in [("MuPDF", mupdf_image), ("Poppler", poppler_image)]:
                        sx, sy = image.width / float(page.mediabox.width), image.height / height
                        crop = image.convert("RGB").crop((round((x0 + 2) * sx), round((height - y1 + 2) * sy),
                                                           round((x1 - 2) * sx), round((height - y0 - 2) * sy)))
                        pixels = crop.tobytes()
                        dark = sum(1 for i in range(0, len(pixels), 3) if max(pixels[i:i + 3]) < 150)
                        check(dark >= 3, f"{name}: stored value is invisible in {renderer}")
    views.close()


def main():
    directory = Path(sys.argv[1]).resolve()
    manifest = json.loads((directory / "manifest.json").read_text())
    failures = []
    for spec in manifest:
        try:
            validate(directory, spec)
            print("PASS", spec["file"])
        except Exception as error:
            failures.append((spec["file"], str(error)))
            print("FAIL", spec["file"], error)

    # Exercise a real edit/save/reopen, including both widgets of the shared headers.
    try:
        spec = next(s for s in manifest if s["file"] == "pd3-nl-NL.pdf").copy()
        writer = PdfWriter(clone_from=directory / spec["file"])
        spec.update(file="pd3-edit-roundtrip.pdf", name="Edited Example", date="2026-09-08",
                    count="9", signature="Edited Example", remarks="Edited after downloading.")
        values = {"name": spec["name"], "date": spec["date"], "package_count": spec["count"],
                  "signature": spec["signature"], "remarks": spec["remarks"]}
        writer.update_page_form_field_values(None, values, auto_regenerate=False)
        writer.write(directory / spec["file"])
        validate(directory, spec)
        print("PASS edit/save/reopen roundtrip")
    except Exception as error:
        failures.append(("edit roundtrip", str(error)))
        print("FAIL edit/save/reopen roundtrip", error)
    export_failures = sum(1 for name, _ in failures if name != "edit roundtrip")
    print(f"{len(manifest) - export_failures}/{len(manifest)} exports passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
