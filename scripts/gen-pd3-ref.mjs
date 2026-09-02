import { writeFileSync } from "node:fs";
import { createChecklistPdf } from "../lib/create-checklist-pdf.ts";
import { getPosition, allItems } from "../lib/checklist-positions.ts";

const position = getPosition("pd3");
const checks = Object.fromEntries(allItems(position).map((item) => [item.id, true]));
const bytes = await createChecklistPdf({
  positionId: "pd3",
  date: "2026-09-02",
  name: "twtst",
  checks,
  count: "3",
  remarks: "test remark",
  sectionRemarks: {},
  evidencePhoto: "",
  signature: "twtst",
  drawn: "",
});
writeFileSync("c:/Users/gilto/Downloads/UPS_pd3_node_ref.pdf", Buffer.from(bytes));
console.log(`Wrote ${bytes.length} bytes`);
