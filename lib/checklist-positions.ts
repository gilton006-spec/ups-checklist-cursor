import source from "./workbook-source.json";

export type CheckItem = { id: string; text: string; note?: string; sourceCell: string };
export type ReferenceImage = { file: string; label: string };
export type CheckSection = { id: string; title: string; items: CheckItem[]; images?: ReferenceImage[]; note?: string; remarksKey?: string };
export type Position = { id: string; label: string; title: string; sourceSheet: string; schedule?: "monday" | "other-days"; scheduleLabel?: string; sections: CheckSection[]; packageLabel: string; handover?: string; sourceNotes?: string[] };
export type PositionDraft = { checks: Record<string, boolean>; count: string; remarks: string; sectionRemarks: Record<string, string>; evidencePhoto: string; signature: string; drawn: string; signatureMode: "type" | "draw" };
export const emptyDraft = (): PositionDraft => ({ checks: {}, count: "", remarks: "", sectionRemarks: {}, evidencePhoto: "", signature: "", drawn: "", signatureMode: "type" });

const sheets = source.sheets;
function sheet(name: string) {
  const found = sheets.find(s => s.name === name);
  if (!found) throw new Error(`Missing source sheet: ${name}`);
  return found;
}
function cell(name: string, address: string) {
  const value = (sheet(name).cells as Record<string, string | undefined>)[address];
  if (!value) throw new Error(`Missing source instruction: ${name}!${address}`);
  return value.trim();
}
function item(name: string, address: string, noteAddress?: string): CheckItem {
  return { id: address.toLowerCase(), text: cell(name, address), sourceCell: address, ...(noteAddress ? { note: cell(name, noteAddress) } : {}) };
}
const ref = (file: string, label: string): ReferenceImage => ({ file, label });
function before(name: string): CheckSection {
  return { id: "before", title: "Before the sort", items: [item(name, "B8"), item(name, "B10"), item(name, "B12", name === "Ps1-Ps2 monday only" ? "F12" : undefined)] };
}
function base(name: string, id: string, label: string) {
  return { id, label, title: cell(name, "E3"), sourceSheet: name };
}
function ps(name: string, id: string, label: string, image: string, firstRow: number): Position {
  return { ...base(name, id, label), schedule: "other-days", scheduleLabel: "Other days", packageLabel: cell(name, `B${firstRow + 6}`), sections: [before(name), { id: "after", title: "After the sort", images: [ref(image, `${label} conveyor diagram`)], items: [item(name, `B${firstRow}`), item(name, `B${firstRow + 2}`), item(name, `B${firstRow + 4}`)] }] };
}
function pd(name: string, id: string, label: string, image: string): Position {
  const finish = id === "pd3" ? 37 : 36;
  return { ...base(name, id, label), ...(id === "pd2" ? { schedule: "other-days" as const, scheduleLabel: "Not on Monday" } : {}), packageLabel: cell(name, "G29"), sections: [before(name), { id: "after", title: "After the sort", images: [ref(image, `${label} conveyor diagram`)], items: [item(name, "B25"), item(name, "F25")] }, { id: "floor", title: "Work floor", images: [ref("image4.png", "First part of the metro belt")], items: [item(name, "B27")] }, { id: "finish", title: "Finish the checks", items: [item(name, `B${finish}`), item(name, `B${finish + 2}`)] }] };
}
const monday = "Ps1-Ps2 monday only";
const smalls = "Smalls";
const matrix = "Matrix";

export const positions: Position[] = [
  { ...base(monday, "ps1-ps2-monday", "PS1 / PS2"), schedule: "monday", scheduleLabel: "Monday only", packageLabel: cell(monday, "B37"), sourceNotes: ['The Monday sheet says "When you start" beside its after-sort clean-position check (B35/F35). This wording is preserved; confirm the intended timing with your team leader.'], sections: [before(monday), { id: "ps1", title: "After the sort: PS1", images: [ref("image2.png", "PS1 conveyor diagram")], items: [item(monday, "B20"), item(monday, "B22")] }, { id: "ps2", title: "After the sort: PS2", images: [ref("image3.png", "PS2 conveyor diagram")], items: [item(monday, "B30"), item(monday, "B32")] }, { id: "finish", title: "Finish the checks", note: "Confirm timing: the source labels this after-sort check ‘When you start’.", items: [item(monday, "B35", "F35")] }] },
  ps("Ps1 other days", "ps1", "PS1", "image2.png", 20),
  ps("Ps2 other days", "ps2", "PS2", "image3.png", 21),
  pd("PD1", "pd1", "PD1", "image5.png"),
  pd("PD2 Not on Monday", "pd2", "PD2", "image6.png"),
  pd("PD3", "pd3", "PD3", "image7.png"),
  pd("PD4", "pd4", "PD4", "image8.png"),
  { ...base(smalls, "smalls", "Smalls"), packageLabel: cell(smalls, "B82"), handover: cell(smalls, "B90"), sourceNotes: ["The source places the walk-off instruction under Before the sort, although it explicitly says to check after the sort. The original wording is preserved.", "The paper handover instruction says to sign the first page, but the signature boxes are on the second page. Confirm the digital handover process; downloading does not send the report."], sections: [before(smalls), { id: "sls1", title: "After the sort: SLS1", images: [ref("image9.png", "SLS1 marked conveyor diagram")], items: [item(smalls, "B30"), item(smalls, "F30"), item(smalls, "B34"), item(smalls, "B36"), item(smalls, "B38"), item(smalls, "B40")] }, { id: "sls1-under", title: "SLS1: under and around", images: [ref("image11.png", "SLS1 marked area to check under and around")], items: [item(smalls, "A32")], remarksKey: "sls1" }, { id: "sls2", title: "After the sort: SLS2", images: [ref("image10.png", "SLS2 marked conveyor diagram")], items: [68, 70, 72, 74, 76, 78, 80].map(row => item(smalls, `B${row}`)) }] },
  { ...base(matrix, "matrix", "Matrix"), packageLabel: cell(matrix, "E84"), sections: [before(matrix), { id: "recirculation", title: "After the sort: recirculation", images: [ref("image12.png", "Matrix recirculation diagram")], items: [item(matrix, "B30"), item(matrix, "B32", "C32"), item(matrix, "B34", "C34"), item(matrix, "B36", "C36")], remarksKey: "recirculation" }, { id: "da", title: "DA platform", images: [ref("image13.png", "DA platform belt diagram")], items: [53, 55, 57, 59].map(row => item(matrix, `B${row}`)) }, { id: "chutes", title: "Chutes and collector belts", images: [ref("image14.png", "Matrix chute reference photo")], items: [item(matrix, "B74"), item(matrix, "B76"), item(matrix, "B83")] }] },
];

export const getPosition = (id: string) => positions.find(p => p.id === id);
export const allItems = (position: Position) => position.sections.flatMap(s => s.items);
export function scheduleWarning(position: Position, date: string): string | undefined {
  if (!date || !position.schedule) return;
  const mondaySelected = new Date(`${date}T12:00:00Z`).getUTCDay() === 1;
  if (position.schedule === "monday" && !mondaySelected) return "This combined PS1 / PS2 checklist is marked Monday only in the workbook. Check your selected date or assignment.";
  if (position.schedule === "other-days" && mondaySelected) return position.id === "pd2" ? "PD2 is marked Not on Monday in the workbook. Confirm your assignment with your team leader." : "For Monday, the workbook provides the combined PS1 / PS2 checklist. Check your selected position.";
}
