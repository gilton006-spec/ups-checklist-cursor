import assert from "node:assert/strict";
import test from "node:test";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import ts from "typescript";
import { jpegPhoto } from "./evidence-fixture.mjs";

async function loadModule(name) {
  const path = new URL(`../lib/${name}.ts`, import.meta.url);
  const result = {};
  const source = ts.transpileModule(await readFile(path, "utf8"), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
  new Function("require", "exports", source)(createRequire(path), result);
  return result;
}
const { handoverNumber, handoverChatUrl, reportFilename, supportsFileShare, shareReport, prepareReport } = await loadModule("whatsapp-handover");
const { validEvidencePhoto } = await loadModule("evidence-validation");
const file = new File(["test PDF"], "test.pdf", { type: "application/pdf" });

test("chat targets exactly the supplied number without putting report data in the URL", () => {
  const url = new URL(handoverChatUrl);
  assert.equal(url.origin, "https://wa.me");
  assert.equal(url.pathname, "/31626149058");
  assert.equal(handoverNumber, "+31 626149058");
  assert.equal(url.searchParams.get("text"), "Sunrise checklist. I will attach the report PDF here.");
  assert.equal(reportFilename("pd4", "2026-09-02"), "UPS_pd4_2026-09-02.pdf");
});

test("unsupported or blocked sharing keeps the save-and-attach fallback", async () => {
  for (const nav of [{}, { share() {}, canShare() { return false; } }, { share() {}, canShare() { throw new Error("Blocked"); } }]) {
    assert.equal(supportsFileShare(file, nav), false);
    assert.equal(await shareReport(file, nav), "unsupported");
  }
});

test("sharing passes the PDF once and never reports successful delivery", async () => {
  let calls = 0;
  const result = await shareReport(file, { canShare: () => true, async share(data) { calls++; assert.deepEqual(data.files, [file]); assert.equal(data.url, undefined); } });
  assert.equal(calls, 1);
  assert.equal(result, "returned");
});

test("cancelling sharing causes no retry or automatic message", async () => {
  let calls = 0;
  assert.equal(await shareReport(file, { canShare: () => true, async share() { calls++; throw new DOMException("Cancelled", "AbortError"); } }), "cancelled");
  assert.equal(calls, 1);
  assert.equal(await shareReport(file, { canShare: () => true, async share() { throw new Error("Permission denied"); } }), "failed");
});

test("preparing sends only to the site's PDF endpoint and verifies the response", async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async (url, init) => {
      assert.equal(url, "/api/download"); assert.equal(init.method, "POST"); assert.equal(init.body.get("checklist"), "sample");
      return new Response("%PDF-1.7", { headers: { "Content-Type": "application/pdf" } });
    };
    const result = await prepareReport("sample", "example.pdf", new AbortController().signal);
    assert.equal(result.name, "example.pdf"); assert.equal(result.type, "application/pdf");
    for (const response of [new Response("Failed", { status: 400 }), new Response("<html>Sign in</html>", { headers: { "Content-Type": "text/html" } })]) {
      globalThis.fetch = async () => response;
      await assert.rejects(prepareReport("sample", "example.pdf", new AbortController().signal));
    }
  } finally { globalThis.fetch = original; }
});

test("photo headers are bounded before image decompression", async () => {
  assert.ok(validEvidencePhoto("")); assert.ok(validEvidencePhoto(jpegPhoto));
  const png = await readFile(new URL("../public/workbook/image13.png", import.meta.url));
  assert.ok(validEvidencePhoto("data:image/png;base64," + png.toString("base64")));
  const huge = Buffer.from(png); huge.writeUInt32BE(100000, 16);
  assert.equal(validEvidencePhoto("data:image/png;base64," + huge.toString("base64")), false);
  for (const bad of ["data:image/jpeg;base64,AAAA", "data:image/png;base64,AAAA", "data:text/html;base64,AAAA"]) assert.equal(validEvidencePhoto(bad), false);
});
