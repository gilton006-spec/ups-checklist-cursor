"use client";

import { useEffect, useState } from "react";
import { Download, MessageCircle, Share2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { handoverNumber, handoverChatUrl, prepareReport, reportFilename, shareReport, supportsFileShare } from "@/lib/whatsapp-handover";

export function WhatsAppHandover({ payload, positionId, date, hasPhoto, disabled }: { payload: string; positionId: string; date: string; hasPhoto: boolean; disabled?: boolean }) {
  const [open, setOpen] = useState(false);
  const [prepared, setPrepared] = useState<{ file: File; url: string; canShare: boolean } | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [sharing, setSharing] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    setPrepared(null); setError(""); setMessage("");
    if (!open) return;
    const controller = new AbortController();
    let objectUrl: string | undefined;
    prepareReport(payload, reportFilename(positionId, date), controller.signal).then(file => {
      if (controller.signal.aborted) return;
      objectUrl = URL.createObjectURL(file);
      setPrepared({ file, url: objectUrl, canShare: supportsFileShare(file, navigator) });
    }).catch(() => {
      if (!controller.signal.aborted) setError("Could not prepare the PDF. Your entries are still here.");
    });
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [open, payload, positionId, date, attempt]);

  async function share() {
    if (!prepared || sharing) return;
    setSharing(true);
    const result = await shareReport(prepared.file, navigator);
    setSharing(false);
    setMessage(result === "cancelled" ? "Sharing cancelled. Nothing was confirmed sent." : result === "returned" ? "Check WhatsApp to confirm the recipient and send." : "Save the PDF, then attach it in the WhatsApp chat.");
  }

  return <Dialog open={open} onOpenChange={setOpen}>
    <DialogTrigger asChild><Button type="button" className="whatsapp-button" disabled={disabled}><MessageCircle />WhatsApp</Button></DialogTrigger>
    <DialogContent className="handover-dialog">
      <DialogHeader><DialogTitle>WhatsApp handover</DialogTitle><DialogDescription>To {handoverNumber}. You choose and confirm sending in WhatsApp.</DialogDescription></DialogHeader>
      {!prepared && !error && <p role="status">Preparing your PDF…</p>}
      {error && <div role="alert"><p>{error}</p><Button type="button" variant="outline" onClick={() => setAttempt(n => n + 1)}>Try again</Button></div>}
      {prepared && <>
        <p className="handover-file">{prepared.file.name}{hasPhoto && <span>Evidence photo included</span>}</p>
        {prepared.canShare && <><Button type="button" className="whatsapp-button" onClick={share} disabled={sharing}><Share2 />{sharing ? "Sharing…" : "Share PDF"}</Button><p className="handover-hint">Choose WhatsApp, then select {handoverNumber}.</p></>}
        <div className="handover-actions">
          <Button variant="outline" asChild><a href={prepared.url} download={prepared.file.name}><Download />Save PDF</a></Button>
          <Button variant="outline" asChild><a href={handoverChatUrl} target="_blank" rel="noopener noreferrer"><MessageCircle />Open WhatsApp chat</a></Button>
        </div>
        <p className="handover-hint">Or save the PDF, open the chat, and attach it. Opening a chat does not attach or send the report.</p>
      </>}
      {message && <p role="status" className="handover-hint">{message}</p>}
    </DialogContent>
  </Dialog>;
}
