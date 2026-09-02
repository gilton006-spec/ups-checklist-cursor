import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const imgSrc = fs.readFileSync(path.join(root, "lib/workbook-images.ts"), "utf8");
const imgMatch = imgSrc.match(/export const workbookImages[^=]*=\s*(\{[\s\S]*\});/);
const images = Function(`return ${imgMatch[1]}`)();

for (const target of [
  path.join(root, "dotnet/UpsChecklist.Web/wwwroot/workbook"),
  path.join(root, "dotnet/UpsChecklist.Core/Assets/Workbook"),
  path.join(root, "public/workbook"),
]) {
  fs.mkdirSync(target, { recursive: true });
  for (const [name, b64] of Object.entries(images)) {
    fs.writeFileSync(path.join(target, name), Buffer.from(b64, "base64"));
  }
}

const fontSrc = fs.readFileSync(path.join(root, "lib/checklist-font.ts"), "utf8");
const fontMatch = fontSrc.match(/export const checklistFont\s*=\s*"([^"]+)"/);
const fontDir = path.join(root, "dotnet/UpsChecklist.Core/Assets");
fs.mkdirSync(fontDir, { recursive: true });
fs.writeFileSync(path.join(fontDir, "checklist-font.ttf"), Buffer.from(fontMatch[1], "base64"));
console.log(`Extracted ${Object.keys(images).length} images and font.`);
