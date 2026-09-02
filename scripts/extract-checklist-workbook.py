"""Read the supplied workbook without modifying it; preserve cells and image anchors."""
import base64
import hashlib
import json
from pathlib import Path
import posixpath
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

SOURCE = Path(sys.argv[1])
ROOT = Path(__file__).resolve().parents[1]
NS = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main", "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "a": "http://schemas.openxmlformats.org/drawingml/2006/main", "xdr": "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"}

with ZipFile(SOURCE) as z:
    def xml(path):
        return ET.fromstring(z.read(path))

    def relations(path):
        rel_path = posixpath.join(posixpath.dirname(path), "_rels", posixpath.basename(path) + ".rels")
        if rel_path not in z.namelist():
            return {}
        return {r.attrib["Id"]: posixpath.normpath(posixpath.join(posixpath.dirname(path), r.attrib["Target"])) for r in xml(rel_path)}

    strings = ["".join(t.text or "" for t in s.findall(".//s:t", NS)) for s in xml("xl/sharedStrings.xml")]
    workbook_rels = relations("xl/workbook.xml")
    sheets = []
    images = {}
    for sheet in xml("xl/workbook.xml").findall("s:sheets/s:sheet", NS):
        path = workbook_rels[sheet.attrib[f"{{{NS['r']}}}id"]]
        root = xml(path)
        cells = {}
        for cell in root.findall(".//s:c", NS):
            value = cell.find("s:v", NS)
            if value is not None:
                cells[cell.attrib["r"]] = strings[int(value.text)] if cell.attrib.get("t") == "s" else value.text
        anchors = []
        for drawing in root.findall("s:drawing", NS):
            drawing_path = relations(path)[drawing.attrib[f"{{{NS['r']}}}id"]]
            drawing_rels = relations(drawing_path)
            for anchor in xml(drawing_path):
                blip = anchor.find(".//a:blip", NS)
                if blip is None:
                    continue
                media = drawing_rels[blip.attrib[f"{{{NS['r']}}}embed"]]
                filename = posixpath.basename(media)
                images[filename] = z.read(media)
                origin = anchor.find("xdr:from", NS)
                crop = anchor.find(".//a:srcRect", NS)
                anchors.append({"file": filename, "row": int(origin.find("xdr:row", NS).text) + 1, "col": int(origin.find("xdr:col", NS).text) + 1, "crop": crop.attrib if crop is not None else {}})
        sheets.append({"name": sheet.attrib["name"], "state": sheet.attrib.get("state", "visible"), "cells": cells, "images": anchors})

    data = {"source": SOURCE.name, "sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(), "sheets": sheets}
    (ROOT / "lib/workbook-source.json").write_text(json.dumps(data, indent=2) + "\n")
    asset_dir = ROOT / "public/workbook"
    asset_dir.mkdir(exist_ok=True)
    for filename, content in images.items():
        (asset_dir / filename).write_bytes(content)
    encoded = {name: base64.b64encode(content).decode() for name, content in images.items()}
    (ROOT / "lib/workbook-images.ts").write_text("// Extracted unchanged from the supplied workbook. Server PDF use only.\nexport const workbookImages: Record<string, string> = " + json.dumps(encoded) + ";\n")
    print(json.dumps([{ "sheet": s["name"], "state": s["state"], "images": s["images"]} for s in sheets], indent=2))
