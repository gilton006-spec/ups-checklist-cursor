"""Verify canonical fields, widgets and page bounds; render all nine PDF samples."""
from pathlib import Path
import json
import subprocess
import sys
from pypdf import PdfReader, PdfWriter
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
qa = root / "outputs/qa"
preview = Path(sys.argv[1])
preview.parent.mkdir(parents=True, exist_ok=True)
writer = PdfWriter()
order = ["ps1-ps2-monday", "ps1", "ps2", "pd1", "pd2", "pd3", "pd4", "smalls", "matrix"]
renders = []
for position in order:
    path = qa / f"{position}.pdf"
    data = json.loads((qa / f"{position}.json").read_text())
    reader = PdfReader(path)
    fields = reader.get_fields()
    assert fields["name"]["/V"] == data["name"]
    assert fields["date"]["/V"] == data["date"]
    assert fields["signature"]["/V"] == data["signature"]
    for name, checked in data["checks"].items():
        assert (fields[name].get("/V", "/Off") != "/Off") == checked
    widgets = 0
    for page in reader.pages:
        for ref in page.get("/Annots", []):
            annotation = ref.get_object()
            if annotation.get("/Subtype") != "/Widget":
                continue
            widgets += 1
            parent = annotation.get("/Parent", ref).get_object()
            name = parent.get("/T", annotation.get("/T"))
            assert name in fields
            assert parent.get("/V", "/Off") == fields[name].get("/V", "/Off")
            assert annotation.get("/AP", {}).get("/N") is not None
            x0, y0, x1, y1 = annotation["/Rect"]
            assert 0 <= x0 < x1 <= float(page.mediabox.width)
            assert 45 <= y0 < y1 <= float(page.mediabox.height)
    print(f"{position}: {len(reader.pages)} pages, {len(fields)} fields, {widgets} verified widgets")
    reader.add_form_topname(position)
    writer.append(reader, outline_item=position.upper())
    subprocess.run(["pdftoppm", "-scale-to", "1200", "-png", str(path), str(qa / position)], check=True, capture_output=True)
    renders.extend(sorted(qa.glob(f"{position}-[0-9]*.png")))

writer.add_metadata({"/Title": "UPS all positions preview", "/Subject": "Fictional demonstration data. Not UPS approved."})
with preview.open("wb") as f:
    writer.write(f)
combined = PdfReader(preview)
assert len(combined.pages) == sum(len(PdfReader(qa / f"{p}.pdf").pages) for p in order)
assert len(combined.get_fields()) >= 9 * 5

# Small contact sheets are inspection aids, not replacements for the source images.
for group in range(0, len(renders), 4):
    canvas = Image.new("RGB", (1200, 1740), "#dddddd")
    draw = ImageDraw.Draw(canvas)
    for index, path in enumerate(renders[group:group + 4]):
        image = Image.open(path).convert("RGB")
        image.thumbnail((582, 830))
        x, y = (index % 2) * 600 + 9, (index // 2) * 870 + 28
        draw.text((x, y - 20), path.name, fill="black")
        canvas.paste(image, (x, y))
    canvas.save(qa / f"contact-{group // 4 + 1}.png")
print(f"Preview saved: {preview}")
