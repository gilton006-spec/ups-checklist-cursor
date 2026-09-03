#!/usr/bin/env python3
"""Independent PDF form/text/layout checks. Requires pypdf and PyMuPDF.

Usage: python verify-pdfs.py <ScannerRegressionTests output directory>
"""
import json
import sys
from pathlib import Path
import fitz
from pypdf import PdfReader

directory = Path(sys.argv[1])
manifest = json.loads((directory / "manifest.json").read_text())
pages_checked = 0
for item in manifest:
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
            assert widget["/AP"]["/N"].get_object().get_data(), "Missing visible field appearance"
            assert name not in widgets_seen, "Unexpected duplicate widget"
            widgets_seen.add(name)
            x0, y0, x1, y1 = map(float, widget["/Rect"])
            assert 32 <= x0 < x1 <= 810 and 48 <= y0 < y1 <= 510, (path.name, name, "Field outside content area")
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
            assert abs(page.rect.width - 841.89) < 0.1 and abs(page.rect.height - 595.28) < 0.1
            for block in page.get_text("dict")["blocks"]:
                if block["type"] != 0:
                    continue
                for line in block["lines"]:
                    for span in line["spans"]:
                        x0, y0, x1, y1 = span["bbox"]
                        assert 25 <= x0 < x1 <= 817 and 12 <= y0 < y1 < 579, (path.name, span, "Text outside printable area")
            # Render every page to force independent appearance parsing.
            page.get_pixmap(matrix=fitz.Matrix(0.75, 0.75), alpha=False)
        if path.name == "long-fields.pdf":
            # Long unbroken text must not be visually truncated while /V remains complete.
            compact = "".join(text.split())
            assert "W" * 300 in compact, "Long comments were clipped"
print(f"PASS: {len(manifest)} PDFs, {pages_checked} pages; all source rows, canonical fields, widgets, appearances and bounds verified.")
