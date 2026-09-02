import { PDFDocument } from "pdf-lib";
import fontkit from "@pdf-lib/fontkit";
import { checklistFont } from "./checklist-font";
import { checklistTemplate } from "./checklist-template";
export type ChecklistData={date:string;name:string;checks:Record<string,boolean>;count:string;remarks:string;signature:string;drawn:string};
export const checkIds=["tape_for_repacking","on_time","clean_at_start","check_belts","check_chutes","check_work_floor","return_tape","clean_at_finish"];
export async function createChecklistPdf(data:ChecklistData) {
  const pdf=await PDFDocument.load(Uint8Array.from(atob(checklistTemplate),c=>c.charCodeAt(0)));
  pdf.registerFontkit(fontkit);
  const form=pdf.getForm();const font=await pdf.embedFont(Uint8Array.from(atob(checklistFont),c=>c.charCodeAt(0)),{subset:true});
  const values:Record<string,string>={date:data.date ? data.date.split("-").reverse().join("/") : "",name:data.name,package_count:data.count,remarks:data.remarks,signature:data.signature};
  for(const [key,value] of Object.entries(values)){
    const field=form.getTextField(key);
    field.acroField.setDefaultAppearance("/Helv 11 Tf 0.13 0.13 0.13 rg");
    field.setText(value);
    const availableWidth=key==="date"?98:key==="name"?233:240;
    field.setFontSize(key==="remarks"?9:Math.min(11,availableWidth/Math.max(1,font.widthOfTextAtSize(value,1))));
  }
  for(const id of checkIds){const field=form.getCheckBox(id);if(data.checks[id])field.check();else field.uncheck();}
  form.updateFieldAppearances(font);form.flatten();
  if(data.drawn){const png=await pdf.embedPng(data.drawn);const fitted=png.scaleToFit(244,23);pdf.getPages()[0].drawImage(png,{x:310,y:29,width:fitted.width,height:fitted.height});}
  return pdf.save();
}
