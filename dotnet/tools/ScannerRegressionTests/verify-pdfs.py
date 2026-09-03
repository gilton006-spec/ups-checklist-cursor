#!/usr/bin/env python3
"""Independent PDF form/text/layout checks. Requires pypdf and PyMuPDF.

Page bounds come from the layout the writer itself uses (manifest.json "layout"),
so this script cannot drift away from the PDF it checks.

Usage: python verify-pdfs.py <ScannerRegressionTests output directory>
"""
import json
import re
import sys
from pathlib import Path
import fitz
from pypdf import PdfReader

directory = Path(sys.argv[1])
manifest = json.loads((directory / "manifest.json").read_text())
layout, items = manifest["layout"], manifest["pdfs"]
left = layout["margin"]
right = layout["pageWidth"] - layout["margin"]
field_top = layout["contentTop"] - layout["headerRowHeight"]
field_bottom = layout["contentBottom"]
# PyMuPDF measures from the top of the page, the writer from the bottom.
ink_top = 10
ink_bottom = layout["pageHeight"] - layout["footerBaseline"] + 5

CLIP = re.compile(rb"([\d.-]+) ([\d.-]+) ([\d.-]+) ([\d.-]+) re W n")
LINE = re.compile(rb"1 0 0 1 ([\d.-]+) ([\d.-]+) Tm\s*<([0-9A-Fa-f]*)> Tj")

pages_checked = 0
lines_checked = 0
for item in items:
    path = directory / item["file"]
    reader = PdfReader(path, strict=True)
    fields = reader.get_fields()
    sheet, data = item["sheet"], item["data"]
    expected = {}
    for row in sheet["rows"]:
        entry = data["entries"].get(row["id"], {})
        for key in ["handoverTo", "scannerNumber", "comments"] + ([] if row["position"] else ["position"]):
            expected[row["id"] + "_" + key] = entry.get(key, "")
    assert set(fields) == set(expected), (path.name, "Missing or unexpected fields")
    for key, value in expected.items():
        assert fields[key].get("/V", "") == value, (path.name, key, "Incorrect canonical value")
    widgets_seen = set()
    for page in reader.pages:
        for annotation in page.get("/Annots", []):
            widget = annotation.get_object()
            assert widget.get("/Subtype") == "/Widget"
            parent = widget["/Parent"].get_object()
            name = parent["/T"]
            assert parent.get("/V", "") == expected[name]
            appearance = widget["/AP"]["/N"].get_object()
            content = appearance.get_data()
            assert content, "Missing visible field appearance"
            assert name not in widgets_seen, "Unexpected duplicate widget"
            widgets_seen.add(name)
            x0, y0, x1, y1 = map(float, widget["/Rect"])
            assert left <= x0 < x1 <= right, (path.name, name, "Field outside the printed columns")
            assert field_bottom <= y0 < y1 <= field_top, (path.name, name, "Field outside the content area")

            # Every glyph the appearance draws must sit inside the clip box, or the value
            # is stored but invisible on paper. Glyph counts also prove no line was dropped.
            clip = CLIP.search(content)
            assert clip, (path.name, name, "Field appearance does not clip its text")
            cx, cy, cw, ch = (float(value) for value in clip.groups())
            glyphs = 0
            for line in LINE.finditer(content):
                baseline = float(line.group(2))
                drawn = len(line.group(3)) // 4
                glyphs += drawn
                if drawn == 0:
                    continue
                lines_checked += 1
                assert cy <= baseline <= cy + ch, (path.name, name, baseline, "Appearance line outside the visible field box")
                assert cx <= float(line.group(1)) <= cx + cw, (path.name, name, "Appearance line starts outside the visible field box")
            assert glyphs >= len("".join(expected[name].split())), (path.name, name, "Appearance draws fewer glyphs than the stored value")
    assert widgets_seen == set(expected)
    with fitz.open(path) as rendered:
        text = "\n".join(page.get_text() for page in rendered)
        for row in sheet["rows"]:
            assert row["userName"] in text, (path.name, row["userName"], "Missing row label")
            if row["note"]:
                assert row["note"] in text, (path.name, "Missing printed note")
        for note in sheet["instructions"]:
            assert note["text"] in text, (path.name, "Missing safety instruction")
        assert data["date"] in text
        for page in rendered:
            pages_checked += 1
            assert abs(page.rect.width - layout["pageWidth"]) < 0.1 and abs(page.rect.height - layout["pageHeight"]) < 0.1
            for block in page.get_text("dict")["blocks"]:
                if block["type"] != 0:
                    continue
                for line in block["lines"]:
                    for span in line["spans"]:
                        x0, y0, x1, y1 = span["bbox"]
                        assert left - 3 <= x0 < x1 <= right + 3, (path.name, span, "Text outside the printable width")
                        assert ink_top <= y0 < y1 < ink_bottom, (path.name, span, "Text outside the printable height")
            # Render every page to force independent appearance parsing.
            page.get_pixmap(matrix=fitz.Matrix(0.75, 0.75), alpha=False)
print(f"PASS: {len(items)} PDFs, {pages_checked} pages, {lines_checked} appearance lines; "
      "all source rows, canonical fields, widgets, visible appearance text and layout bounds verified.")
