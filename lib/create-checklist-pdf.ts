import { PDFDocument, rgb, type PDFPage } from "pdf-lib";
import fontkit from "@pdf-lib/fontkit";
import { checklistFont } from "./checklist-font";
import { workbookImages } from "./workbook-images";
import { getPosition, scheduleWarning } from "./checklist-positions";

export type ChecklistData = { positionId: string; date: string; name: string; checks: Record<string, boolean>; count: string; remarks: string; sectionRemarks: Record<string, string>; signature: string; drawn: string };
const decode = (value: string) => Uint8Array.from(atob(value), c => c.charCodeAt(0));

export async function createChecklistPdf(data: ChecklistData) {
  const position = getPosition(data.positionId);
  if (!position) throw new Error("Unknown checklist position.");
  const pdf = await PDFDocument.create();
  pdf.registerFontkit(fontkit);
  const font = await pdf.embedFont(decode(checklistFont), { subset: true });
  const form = pdf.getForm();
  const brown = rgb(0.28, 0.17, 0.10);
  const ink = rgb(0.12, 0.12, 0.12);
  const muted = rgb(0.35, 0.35, 0.35);
  const line = rgb(0.72, 0.74, 0.76);
  const W = 595.28, H = 841.89, M = 36, CW = W - M * 2;
  let page!: PDFPage;
  let y = 0;
  const dateField = form.createTextField("date");
  dateField.setText(data.date);
  const nameField = form.createTextField("name");
  nameField.setText(data.name);
  dateField.acroField.setDefaultAppearance("/Helv 10 Tf 0 g");
  nameField.acroField.setDefaultAppearance("/Helv 10 Tf 0 g");
  dateField.setFontSize(10);
  nameField.setFontSize(Math.min(10, 300 / Math.max(1, font.widthOfTextAtSize(data.name, 1))));

  function text(value: string, x: number, baseline: number, size = 10, color = ink) {
    page.drawText(value, { x, y: baseline, size, font, color });
  }
  function wrapped(value: string, width = CW, size = 10) {
    const result: string[] = [];
    for (const paragraph of value.replace(/\r\n?/g, "\n").split("\n")) {
      let current = "";
      for (const char of paragraph) {
        if (current && font.widthOfTextAtSize(current + char, size) > width) {
          const lastSpace = current.lastIndexOf(" ");
          if (lastSpace > current.length * 0.5) {
            result.push(current.slice(0, lastSpace));
            current = current.slice(lastSpace + 1) + char;
          } else { result.push(current); current = char; }
        } else current += char;
      }
      result.push(current);
    }
    return result;
  }
  function newPage() {
    page = pdf.addPage([W, H]);
    page.drawRectangle({ x: 0, y: H - 12, width: W, height: 12, color: brown });
    text("SUNRISE  |  CHECKLIST JAMB. / SING.", M, H - 38, 10, brown);
    text(position!.title, M, H - 62, 19, brown);
    text(position!.scheduleLabel ?? "Position checklist", M, H - 80, 9, muted);
    text("Name:", M, H - 105, 10);
    nameField.addToPage(page, { x: M + 37, y: H - 112, width: 307, height: 22, borderWidth: 0.7, borderColor: line, font });
    text("Date:", 397, H - 105, 10);
    dateField.addToPage(page, { x: 429, y: H - 112, width: 130, height: 22, borderWidth: 0.7, borderColor: line, font });
    y = H - 136;
  }
  function ensure(height: number) { if (y - height < 55) newPage(); }
  function paragraph(value: string, size = 10, color = ink) {
    for (const l of wrapped(value, CW, size)) { ensure(size + 5); text(l, M, y - size, size, color); y -= size + 5; }
    y -= 5;
  }
  function sectionTitle(title: string) {
    ensure(65);
    page.drawRectangle({ x: M, y: y - 25, width: CW, height: 25, color: rgb(0.94, 0.93, 0.91) });
    text(title, M + 9, y - 17, 12, brown); y -= 36;
  }
  function textField(key: string, label: string, value: string, width = CW, height = 28, multiline = false) {
    ensure(height + 33);
    text(label, M, y - 11, 10, brown); y -= 20;
    const field = form.createTextField(key);
    if (multiline) field.enableMultiline();
    field.setText(value);
    field.addToPage(page, { x: M, y: y - height, width, height, borderWidth: 0.7, borderColor: line, font, backgroundColor: rgb(1, 1, 1) });
    field.setFontSize(multiline ? 10 : Math.min(11, (width - 10) / Math.max(1, font.widthOfTextAtSize(value, 1))));
    y -= height + 14;
  }
  function remarks(key: string, label: string, value: string) {
    const lines = wrapped(value, CW - 16, 10);
    // Explicit wrapping and continuation fields prevent long notes from being clipped.
    const chunkSize = 26;
    for (let offset = 0; offset < lines.length; offset += chunkSize) {
      const chunk = lines.slice(offset, offset + chunkSize);
      const suffix = offset ? `_continued_${offset / chunkSize}` : "";
      textField(key + suffix, label + (offset ? " (continued)" : ""), chunk.join("\n"), CW, Math.max(58, chunk.length * 15 + 20), true);
    }
  }

  newPage();
  paragraph("PROTOTYPE FOR REVIEW. Not UPS approved. Downloading does not submit this report.", 9, muted);
  const warning = scheduleWarning(position, data.date);
  if (warning) paragraph(warning, 9, brown);

  for (const section of position.sections) {
    const images = await Promise.all((section.images ?? []).map(async reference => ({ reference, png: await pdf.embedPng(decode(workbookImages[reference.file])) })));
    const firstImage = images[0]?.png.scaleToFit(CW, 165);
    const noteHeight = section.note ? wrapped(section.note, CW, 9).length * 14 + 5 : 0;
    // Keep the heading with its first reference image and check.
    ensure(36 + noteHeight + (firstImage ? firstImage.height + 33 : 0) + 40);
    sectionTitle(section.title);
    if (section.note) paragraph(section.note, 9, brown);
    for (const { reference, png } of images) {
      const size = png.scaleToFit(CW, 165);
      ensure(size.height + 48);
      text(reference.label, M, y - 10, 9, muted); y -= 19;
      page.drawImage(png, { x: M + (CW - size.width) / 2, y: y - size.height, width: size.width, height: size.height });
      y -= size.height + 14;
    }
    for (const item of section.items) {
      const lines = wrapped(item.text, CW - 30, 10.5);
      const notes = item.note ? wrapped(item.note, CW - 30, 9) : [];
      const height = Math.max(25, lines.length * 14 + notes.length * 13 + 10);
      ensure(height);
      const check = form.createCheckBox(item.id);
      check.addToPage(page, { x: M + 1, y: y - 16, width: 14, height: 14, borderWidth: 1, borderColor: muted });
      if (data.checks[item.id]) check.check(); else check.uncheck();
      let baseline = y - 12;
      for (const l of lines) { text(l, M + 26, baseline, 10.5); baseline -= 14; }
      for (const l of notes) { text(l, M + 26, baseline, 9, muted); baseline -= 13; }
      y -= height;
    }
    if (section.remarksKey) remarks(`remarks_${section.remarksKey}`, section.remarksKey === "sls1" ? "SLS1 remarks" : "Recirculation remarks", data.sectionRemarks[section.remarksKey] ?? "");
    y -= 8;
  }

  textField("package_count", position.packageLabel, data.count, 170);
  remarks("remarks", "Other remarks / incomplete checks / actions / follow-up", data.remarks);
  if (position.handover) {
    paragraph("Original paper handover instruction:", 10, brown);
    paragraph(position.handover, 9);
    paragraph("Confirm the digital handover process with your team leader. Downloading does not send the report.", 9, muted);
  }
  if (data.drawn) {
    ensure(85);
    text("Drawn signature (not identity verified)", M, y - 11, 10, brown);
    y -= 20;
    page.drawRectangle({ x: M, y: y - 53, width: CW, height: 53, borderWidth: 0.7, borderColor: line });
    const png = await pdf.embedPng(data.drawn);
    const size = png.scaleToFit(CW - 16, 43);
    page.drawImage(png, { x: M + 8, y: y - 48, width: size.width, height: size.height });
    y -= 67;
  } else textField("signature", "Signature (not identity verified)", data.signature, CW, 53);
  if (position.sourceNotes?.length) {
    sectionTitle("Source instructions to confirm");
    for (const note of position.sourceNotes) paragraph(note, 9, muted);
  }

  const pages = pdf.getPages();
  for (const [index, p] of pages.entries()) {
    p.drawLine({ start: { x: M, y: 43 }, end: { x: W - M, y: 43 }, thickness: 0.6, color: line });
    p.drawText(`Prototype | Source: ${position.sourceSheet}`, { x: M, y: 29, size: 8, font, color: muted });
    p.drawText(`Page ${index + 1} of ${pages.length}`, { x: W - M - 62, y: 29, size: 8, font, color: muted });
  }
  form.updateFieldAppearances(font);
  pdf.setTitle(`Sunrise ${position.label} checklist`);
  pdf.setSubject(`Prototype from source worksheet: ${position.sourceSheet}`);
  pdf.setCreator("Position checklist prototype");
  return pdf.save();
}
