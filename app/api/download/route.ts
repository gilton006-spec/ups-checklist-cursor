import { z } from "zod";
import { createChecklistPdf } from "@/lib/create-checklist-pdf";
const schema=z.object({date:z.string().regex(/^(\d{4}-\d{2}-\d{2})?$/),name:z.string().max(65),checks:z.record(z.boolean()),count:z.string().regex(/^\d{0,5}$/),remarks:z.string().max(300),signature:z.string().max(65),drawn:z.string().max(200000).refine(v=>v==="" || /^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(v))});
export async function POST(request:Request){
  try{
    if(Number(request.headers.get("content-length"))>250000)return new Response("The signature is too large. Go back, clear it and sign again.",{status:413});
    const form=await request.formData();const raw=form.get("checklist");
    if(typeof raw!=="string" || raw.length>240000)return new Response("Please go back and try the download again.",{status:400});
    const data=schema.parse(JSON.parse(raw));
    const bytes=await createChecklistPdf(data);
    return new Response(new Uint8Array(bytes),{headers:{"Content-Type":"application/pdf","Content-Disposition":`attachment; filename="UPS_Checklist_${data.date||"undated"}.pdf"`,"Cache-Control":"no-store","X-Content-Type-Options":"nosniff"}});
  }catch(error){
    const msg=error instanceof Error && error.message.includes("WinAnsi")?"Please go back and use standard Latin letters in the text fields, or draw your signature.":"The PDF could not be created. Your checklist is still open in the original tab. Please return to it and try again.";
    return new Response(msg,{status:400,headers:{"Content-Type":"text/plain; charset=utf-8","Cache-Control":"no-store"}});
  }
}
