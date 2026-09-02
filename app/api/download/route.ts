import { z } from "zod";
import { createChecklistPdf } from "@/lib/create-checklist-pdf";
import { getPosition, allItems } from "@/lib/checklist-positions";
import { validEvidencePhoto } from "@/lib/evidence-validation";
const imageData=z.string().max(800000).refine(validEvidencePhoto);
const maxBodyBytes=1600000;
const schema=z.object({positionId:z.string().refine(v=>!!getPosition(v),"Select a valid position"),date:z.string().regex(/^(\d{4}-\d{2}-\d{2})?$/).refine(v=>!v || (!Number.isNaN(Date.parse(v)) && new Date(v).toISOString().slice(0,10)===v)),name:z.string().max(65),checks:z.record(z.boolean()),count:z.string().regex(/^\d{0,5}$/),remarks:z.string().max(2000),sectionRemarks:z.record(z.string().max(1000)).default({}),evidencePhoto:imageData.default(""),signature:z.string().max(65),drawn:z.string().max(200000).refine(v=>v==="" || /^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(v))}).superRefine((data,ctx)=>{
  const position=getPosition(data.positionId);if(!position)return;
  const allowedChecks=new Set(allItems(position).map(i=>i.id));
  const allowedRemarks=new Set(position.sections.flatMap(s=>s.remarksKey?[s.remarksKey]:[]));
  if(Object.keys(data.checks).some(k=>!allowedChecks.has(k)) || Object.keys(data.sectionRemarks).some(k=>!allowedRemarks.has(k)))ctx.addIssue({code:z.ZodIssueCode.custom,message:"Entries do not match this position"});
});
export async function POST(request:Request){
  try{
    const tooLarge=()=>new Response("The report is too large. Choose a smaller photo and try again.",{status:413,headers:{"Cache-Control":"no-store"}});
    if(Number(request.headers.get("content-length"))>maxBodyBytes)return tooLarge();
    const reader=request.body?.getReader();
    if(!reader)return new Response("The checklist is missing.",{status:400});
    const chunks:Uint8Array[]=[];let size=0;
    while(true){const {done,value}=await reader.read();if(done)break;size+=value.byteLength;if(size>maxBodyBytes){await reader.cancel();return tooLarge();}chunks.push(value);}
    const body=new Uint8Array(size);let offset=0;for(const chunk of chunks){body.set(chunk,offset);offset+=chunk.byteLength;}
    const form=await new Response(body,{headers:{"Content-Type":request.headers.get("content-type")||""}}).formData();const raw=form.get("checklist");
    if(typeof raw!=="string" || raw.length>1040000)return tooLarge();
    const data=schema.parse(JSON.parse(raw));
    const bytes=await createChecklistPdf(data);
    return new Response(new Uint8Array(bytes),{headers:{"Content-Type":"application/pdf","Content-Disposition":`attachment; filename="UPS_${data.positionId}_${data.date||"undated"}.pdf"`,"Cache-Control":"no-store","X-Content-Type-Options":"nosniff"}});
  }catch(error){
    const msg=error instanceof Error && error.message.includes("WinAnsi")?"Please go back and use standard Latin letters in the text fields, or draw your signature.":"The PDF could not be created. Your checklist is still open in the original tab. Please return to it and try again.";
    return new Response(msg,{status:400,headers:{"Content-Type":"text/plain; charset=utf-8","Cache-Control":"no-store"}});
  }
}
