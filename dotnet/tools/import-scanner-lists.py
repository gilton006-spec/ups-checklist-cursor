#!/usr/bin/env python3
"""Read the supplied XLSX forms without Excel or third-party packages.

Print deterministic JSON to stdout. Run with the Monday and Tuesday-Friday paths,
in that order. The resulting catalog is embedded in UpsChecklist.Core.
Only form data is retained, not document author metadata or cached dates.
"""
import hashlib
import json
import posixpath
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

NS = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
HEADERS = ["User name", "Gost", "Scan. Need.", "Position", "Handover to", "Scanner#", "Comments"]


def extract(path, version, label):
    with zipfile.ZipFile(path) as book:
        strings = ["".join(n.itertext()) for n in ET.fromstring(book.read("xl/sharedStrings.xml"))]
        relationships = {r.get("Id"): r.get("Target") for r in ET.fromstring(book.read("xl/_rels/workbook.xml.rels"))}
        sheets = []
        for sheet in ET.fromstring(book.read("xl/workbook.xml")).findall("s:sheets/s:sheet", NS):
            target = relationships[sheet.get("{" + REL + "}id")]
            target = target.lstrip("/") if target.startswith("/") else posixpath.normpath("xl/" + target)
            root = ET.fromstring(book.read(target))
            cells = {}
            for c in root.findall("s:sheetData/s:row/s:c", NS):
                v = c.find("s:v", NS)
                value = v.text if v is not None else ""
                if c.get("t") == "s":
                    value = strings[int(value)]
                elif c.get("t") == "inlineStr":
                    value = "".join(c.find("s:is", NS).itertext())
                cells[c.get("r")] = value or ""
            start = "A" if cells.get("A3") == "User name" else "B"
            columns = [chr(ord(start) + i) for i in range(7)]
            assert [cells.get(c + "3", "") for c in columns] == HEADERS, "Unknown scanner form columns"
            rows, instructions = [], []
            for row in root.findall("s:sheetData/s:row", NS):
                number = int(row.get("r"))
                if number <= 3:
                    continue
                values = [cells.get(c + str(number), "") for c in columns]
                if values[2] in ("One piece", "Two piece"):
                    assert values[0], "Scanner assignment must have a user name"
                    rows.append(dict(zip(
                        ["userName", "gost", "scannerType", "position", "handoverTo", "scannerNumber", "note"], values),
                        id="r" + str(number), sourceRow=number))
                elif any(values):
                    instructions.extend(dict(text=v, sourceCell=c + str(number)) for c, v in zip(columns, values) if v)
            title = cells[columns[-1] + "1"]
            sheets.append(dict(id=sheet.get("name").lower(), sourceSheet=sheet.get("name"), title=title,
                               headers=HEADERS, rows=rows, instructions=instructions))
        return dict(id=version, label=label, sourceFile=path.name,
                    sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(), sheets=sheets)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("Usage: import-scanner-lists.py Monday.xlsx Tuesday-Friday.xlsx")
    result = [extract(Path(sys.argv[1]), "monday", "Monday"),
              extract(Path(sys.argv[2]), "tuesday-friday", "Tuesday / Friday")]
    print(json.dumps(result, ensure_ascii=False, indent=2))
