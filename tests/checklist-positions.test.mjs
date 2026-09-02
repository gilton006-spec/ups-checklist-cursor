import assert from "node:assert/strict";
import test from "node:test";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import ts from "typescript";
import { PDFDocument, PDFName, PDFRawStream } from "pdf-lib";
import { jpegPhoto } from "./evidence-fixture.mjs";

// Exercise the same position definitions used by the UI and the PDF endpoint.
const path = new URL("../lib/checklist-positions.ts", import.meta.url);
const compiled = ts.transpileModule(await readFile(path, "utf8"), { compilerOptions: { module: ts.ModuleKind.CommonJS, esModuleInterop: true } });
const exports = {};
new Function("require", "exports", compiled.outputText)(createRequire(path), exports);
const { positions, allItems, scheduleWarning, emptyDraft } = exports;
const { default: worker } = await import("../dist/server/index.js");
const env = { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } };
const ctx = { waitUntil() {}, passThroughOnException() {} };
const output = new URL("../outputs/qa/", import.meta.url);
await mkdir(output, { recursive: true });

function dataFor(position) {
  return { positionId: position.id, date: position.schedule === "monday" ? "2026-09-07" : "2026-09-02", name: "DEMO José Example", checks: Object.fromEntries(allItems(position).map((i, index) => [i.id, index % 2 === 0])), count: "7", remarks: "DEMO: One check needs follow-up. Team leader review required.", sectionRemarks: Object.fromEntries(position.sections.filter(s => s.remarksKey).map(s => [s.remarksKey, "DEMO: Area reviewed; follow-up recorded."])), signature: "José Example", drawn: "" };
}
async function request(data) {
  return worker.fetch(new Request("http://localhost/api/download", { method: "POST", body: new URLSearchParams({ checklist: JSON.stringify(data) }) }), env, ctx);
}

test("all nine source worksheets are represented, with exact cell text and correct images", async () => {
  const source = JSON.parse(await readFile(new URL("../lib/workbook-source.json", import.meta.url), "utf8"));
  assert.equal(positions.length, 9);
  assert.equal(new Set(positions.map(p => p.id)).size, 9);
  assert.deepEqual(positions.map(p => p.sourceSheet), source.sheets.map(s => s.name));
  for (const position of positions) {
    const sheet = source.sheets.find(s => s.name === position.sourceSheet);
    const items = allItems(position);
    assert.equal(new Set(items.map(i => i.id)).size, items.length);
    for (const item of items) assert.equal(item.text, sheet.cells[item.sourceCell].trim());
    const expected = new Set(sheet.images.filter(i => i.row > 1).map(i => i.file));
    const actual = new Set(position.sections.flatMap(s => s.images ?? []).map(i => i.file));
    assert.deepEqual(actual, expected);
  }
  assert.deepEqual(positions.map(p => allItems(p).length), [8, 6, 6, 8, 8, 8, 8, 17, 14]);
});

test("Monday rules warn without silently changing a selected position", () => {
  assert.ok(scheduleWarning(positions[0], "2026-09-02"));
  assert.equal(scheduleWarning(positions[0], "2026-09-07"), undefined);
  for (const id of ["ps1", "ps2", "pd2"]) assert.ok(scheduleWarning(positions.find(p => p.id === id), "2026-09-07"));
  assert.equal(scheduleWarning(positions.find(p => p.id === "pd1"), "2026-09-07"), undefined);
});

test("new position drafts have no shared checkbox or remarks objects", () => {
  const first = emptyDraft(), second = emptyDraft();
  first.checks.b8 = true; first.sectionRemarks.sls1 = "first";
  assert.deepEqual(second.checks, {}); assert.deepEqual(second.sectionRemarks, {});
});

test("initial screen exposes all nine position buttons and no preselected position", async () => {
  const response = await worker.fetch(new Request("http://localhost/", { headers: { accept: "text/html" } }), env, ctx);
  assert.equal(response.status, 200);
  const html = await response.text();
  for (const position of positions) assert.ok(html.includes(position.label.replaceAll("&", "&amp;")));
  assert.match(html, /Choose your position/);
  assert.equal((html.match(/aria-pressed="false"/g) ?? []).length, 9);
  assert.match(html, /Demo only\. Not UPS approved/);
  assert.doesNotMatch(html, /Open the right checklist with one tap/);
});

for (const position of positions) test(`${position.label}: endpoint generates the right editable PDF`, async () => {
  const data = dataFor(position);
  const response = await request(data);
  assert.equal(response.status, 200, await (response.status !== 200 ? response.text() : Promise.resolve("")));
  assert.equal(response.headers.get("content-type"), "application/pdf");
  assert.match(response.headers.get("content-disposition"), new RegExp(position.id));
  assert.equal(response.headers.get("cache-control"), "no-store");
  const bytes = new Uint8Array(await response.arrayBuffer());
  const pdf = await PDFDocument.load(bytes);
  const form = pdf.getForm();
  assert.equal(form.getTextField("name").getText(), data.name);
  assert.equal(form.getTextField("date").getText(), data.date);
  assert.equal(form.getTextField("package_count").getText(), "7");
  assert.equal(form.getTextField("signature").getText(), data.signature);
  for (const item of allItems(position)) assert.equal(form.getCheckBox(item.id).isChecked(), data.checks[item.id]);
  const expectedFields = allItems(position).length + 5 + position.sections.filter(s => s.remarksKey).length;
  assert.equal(form.getFields().length, expectedFields);
  assert.match(pdf.getTitle(), new RegExp(position.label.replace(/[.*+?^$\{\}()|[\]\\]/g, "\\$&")));
  await writeFile(new URL(`${position.id}.pdf`, output), bytes);
  await writeFile(new URL(`${position.id}.json`, output), JSON.stringify(data, null, 2));
});

test("long remarks continue without truncation", async () => {
  const data = dataFor(positions.find(p => p.id === "matrix"));
  data.remarks = "Follow-up required. ".repeat(100).slice(0, 2000);
  data.sectionRemarks.recirculation = "Area issue noted. ".repeat(55).slice(0, 1000);
  const response = await request(data);
  assert.equal(response.status, 200);
  const bytes = new Uint8Array(await response.arrayBuffer());
  const pdf = await PDFDocument.load(bytes);
  const parts = pdf.getForm().getFields().filter(f => /^remarks(?:_continued_\d+)?$/.test(f.getName())).map(f => f.getText());
  assert.equal(parts.join(" ").replace(/\s/g, ""), data.remarks.replace(/\s/g, ""));
  await writeFile(new URL("matrix-long.pdf", output), bytes);
});

test("drawn signatures are embedded without a white form widget hiding the image", async () => {
  const data = dataFor(positions.find(p => p.id === "pd4"));
  data.signature = "";
  // A fixture image exercises image embedding; this is not a person's signature.
  data.drawn = "data:image/png;base64," + (await readFile(new URL("../public/workbook/image13.png", import.meta.url))).toString("base64");
  const response = await request(data);
  assert.equal(response.status, 200);
  const bytes = new Uint8Array(await response.arrayBuffer());
  const pdf = await PDFDocument.load(bytes);
  assert.equal(pdf.getForm().getFieldMaybe("signature"), undefined);
  assert.equal(pdf.getForm().getTextField("name").getText(), data.name);
  await writeFile(new URL("pd4-drawn.pdf", output), bytes);
});

test("portrait evidence is embedded with editable form fields intact", async () => {
  const data = dataFor(positions.find(p => p.id === "pd4"));
  data.evidencePhoto = "data:image/png;base64," + (await readFile(new URL("../public/workbook/image13.png", import.meta.url))).toString("base64");
  const response = await request(data);
  assert.equal(response.status, 200);
  const bytes = new Uint8Array(await response.arrayBuffer());
  const pdf = await PDFDocument.load(bytes);
  assert.ok(hasImage(pdf, 104, 372));
  assert.equal(pdf.getPageCount(), 2, "photo and signature should fit together");
  assert.equal(pdf.getForm().getTextField("signature").getText(), data.signature);
  await writeFile(new URL("pd4-evidence.pdf", output), bytes);
});

function hasImage(pdf, width, height) {
  return pdf.context.enumerateIndirectObjects().some(([, object]) => object instanceof PDFRawStream && object.dict.get(PDFName.of("Subtype"))?.toString() === "/Image" && object.dict.get(PDFName.of("Width"))?.asNumber() === width && object.dict.get(PDFName.of("Height"))?.asNumber() === height);
}

test("all nine positions embed JPEG camera evidence", async () => {
  for (const position of positions) {
    const response = await request({ ...dataFor(position), evidencePhoto: jpegPhoto });
    assert.equal(response.status, 200, position.id);
    const bytes = new Uint8Array(await response.arrayBuffer());
    const pdf = await PDFDocument.load(bytes);
    assert.ok(hasImage(pdf, 32, 24), position.id);
    if (position.id === "pd4") await writeFile(new URL("pd4-camera-evidence.pdf", output), bytes);
  }
});

test("unsupported, malformed and oversized evidence is rejected", async () => {
  const data = dataFor(positions[0]);
  for (const evidencePhoto of ["data:image/svg+xml;base64,AAAA", "data:image/jpeg;base64,AAAA", "data:image/png;base64," + "A".repeat(800001)]) assert.equal((await request({ ...data, evidencePhoto })).status, 400);
  const large = new Request("http://localhost/api/download", { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body: "x".repeat(1600001) });
  assert.equal((await worker.fetch(large, env, ctx)).status, 413);
});

test("unknown positions, mismatched fields, invalid dates and overlong remarks are rejected", async () => {
  const data = dataFor(positions[0]);
  for (const invalid of [{ ...data, positionId: "unknown" }, { ...data, checks: { unrelated: true } }, { ...data, date: "2026-02-30" }, { ...data, remarks: "x".repeat(2001) }, { ...data, sectionRemarks: { unrelated: "test" } }]) {
    assert.equal((await request(invalid)).status, 400);
  }
});
