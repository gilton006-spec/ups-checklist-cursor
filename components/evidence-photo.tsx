"use client";

import { useEffect, useRef, useState } from "react";
import { Camera, ImagePlus } from "lucide-react";
import { Button } from "@/components/ui/button";

export function EvidencePhoto({ value, onChange, onBusyChange }: { value: string; onChange: (photo: string) => void; onBusyChange: (busy: boolean) => void }) {
  const camera = useRef<HTMLInputElement>(null);
  const gallery = useRef<HTMLInputElement>(null);
  const request = useRef(0);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => () => { request.current++; }, []);

  async function add(file?: File) {
    if (!file) return;
    setError("");
    if (!file.type.startsWith("image/") || file.type === "image/svg+xml") { setError("Choose a photo, such as a JPEG or PNG."); return; }
    if (file.size > 15 * 1024 * 1024) { setError("Choose a photo smaller than 15 MB."); return; }
    const current = ++request.current;
    setBusy(true); onBusyChange(true);
    const url = URL.createObjectURL(file);
    try {
      const image = new Image();
      await new Promise<void>((resolve, reject) => { image.onload = () => resolve(); image.onerror = reject; image.src = url; });
      if (request.current !== current) return;
      const scale = Math.min(1, 1280 / Math.max(image.naturalWidth, image.naturalHeight));
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
      canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
      const context = canvas.getContext("2d");
      if (!context) throw new Error("Canvas unavailable");
      context.fillStyle = "#fff"; context.fillRect(0, 0, canvas.width, canvas.height);
      context.drawImage(image, 0, 0, canvas.width, canvas.height);
      // Re-encoding also removes original location/camera metadata.
      let photo = canvas.toDataURL("image/jpeg", 0.78);
      if (photo.length > 800000) photo = canvas.toDataURL("image/jpeg", 0.55);
      if (photo.length > 800000 || !photo.startsWith("data:image/jpeg;base64,")) throw new Error("Photo too large");
      onChange(photo);
    } catch {
      if (request.current === current) setError("Could not add this photo. Try a JPEG or PNG.");
    } finally {
      URL.revokeObjectURL(url);
      if (request.current === current) { setBusy(false); onBusyChange(false); }
    }
  }

  return <section className="evidence-photo" aria-label="Evidence photo">
    <p className="field-label">Evidence photo <span className="optional">Optional</span></p>
    <div className="photo-actions"><Button type="button" variant="outline" onClick={() => camera.current?.click()} disabled={busy}><Camera />Take photo</Button><Button type="button" variant="outline" onClick={() => gallery.current?.click()} disabled={busy}><ImagePlus />Choose photo</Button></div>
    <input ref={camera} type="file" accept="image/*" capture="environment" hidden onChange={e => { void add(e.target.files?.[0]); e.currentTarget.value = ""; }} />
    <input ref={gallery} type="file" accept="image/*" hidden onChange={e => { void add(e.target.files?.[0]); e.currentTarget.value = ""; }} />
    {busy && <p role="status">Adding photo…</p>}
    {error && <p className="photo-error" role="alert">{error}</p>}
    {value && <div className="photo-preview"><img src={value} alt="Evidence photo preview" /><Button type="button" variant="outline" disabled={busy} onClick={() => onChange("")}>Remove photo</Button></div>}
    <p className="field-hint">Included in your PDF. Avoid faces and parcel addresses.</p>
  </section>;
}
