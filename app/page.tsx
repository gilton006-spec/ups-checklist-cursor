"use client";

import { useEffect, useRef, useState, type PointerEvent } from "react";
import { Download, Minus, Plus, Sun, PenLine, Type, ChevronDown } from "lucide-react";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { positions, getPosition, allItems, emptyDraft, scheduleWarning, type CheckItem, type PositionDraft } from "@/lib/checklist-positions";

export default function Home() {
  const [date, setDate] = useState("");
  const [name, setName] = useState("");
  const [positionId, setPositionId] = useState("");
  const [drafts, setDrafts] = useState<Record<string, PositionDraft>>({});
  const [downloadStarted, setDownloadStarted] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const position = getPosition(positionId);
  const draft = drafts[positionId] ?? emptyDraft();
  const items = position ? allItems(position) : [];
  const completed = items.filter(item => draft.checks[item.id]).length;
  const warning = position ? scheduleWarning(position, date) : undefined;

  useEffect(() => {
    const d = new Date();
    setDate(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`);
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    ctx?.clearRect(0, 0, 700, 180);
    if (draft.drawn) {
      const img = new Image();
      let active = true;
      img.onload = () => { if (active) ctx?.drawImage(img, 0, 0); };
      img.src = draft.drawn;
      return () => { active = false; };
    }
  }, [positionId, draft.signatureMode, draft.drawn]);

  function changed() { setDownloadStarted(false); }
  function updateDraft(update: Partial<PositionDraft>) {
    setDrafts(previous => ({ ...previous, [positionId]: { ...(previous[positionId] ?? emptyDraft()), ...update } }));
    changed();
  }
  function point(e: PointerEvent<HTMLCanvasElement>) {
    const r = e.currentTarget.getBoundingClientRect();
    return [(e.clientX - r.left) * 700 / r.width, (e.clientY - r.top) * 180 / r.height];
  }
  function startDraw(e: PointerEvent<HTMLCanvasElement>) {
    e.preventDefault(); changed(); drawing.current = true; e.currentTarget.setPointerCapture(e.pointerId);
    const ctx = e.currentTarget.getContext("2d")!;
    const [x, y] = point(e); ctx.strokeStyle = "#20170f"; ctx.fillStyle = "#20170f"; ctx.lineWidth = 2.8; ctx.lineCap = "round"; ctx.lineJoin = "round";
    ctx.beginPath(); ctx.arc(x, y, 1.4, 0, Math.PI * 2); ctx.fill(); ctx.beginPath(); ctx.moveTo(x, y);
  }
  function moveDraw(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return;
    e.preventDefault(); const ctx = e.currentTarget.getContext("2d")!;
    const [x, y] = point(e); ctx.lineTo(x, y); ctx.stroke();
  }
  function endDraw(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return;
    drawing.current = false; updateDraft({ drawn: e.currentTarget.toDataURL("image/png") });
  }
  function row(item: CheckItem) {
    const id = `${positionId}-${item.id}`;
    return <label htmlFor={id} className={`check-row ${draft.checks[item.id] ? "is-checked" : ""}`} key={item.id}>
      <Checkbox id={id} checked={!!draft.checks[item.id]} onCheckedChange={value => updateDraft({ checks: { ...draft.checks, [item.id]: value === true } })} className="check-box" />
      <span className="check-copy">{item.text}{item.note && <span className="check-note">{item.note}</span>}</span>
      {draft.checks[item.id] && <span className="done-label" aria-hidden="true">Done</span>}
    </label>;
  }

  return <main className="page-shell">
    <header className="topbar"><div className="wordmark"><img src="/workbook/image1.png" alt="UPS" width="47" height="47" /><span>Shift <span className="wordmark-light">Checklist</span></span></div><span className="shift"><Sun size={17} /> Sunrise</span></header>
    <div className="intro"><p className="eyebrow">CHECKLIST JAMB. / SING.</p><h1>Choose your position</h1><p>Open the right checklist with one tap.</p></div>
    <div className="prototype-note">Prototype for review. Use fictional details until approved for workplace use.</div>
    <section className="position-grid" aria-label="Jambreaker position">
      {positions.map(p => <Button key={p.id} type="button" variant="outline" className={`position-button ${positionId === p.id ? "selected" : ""}`} aria-pressed={positionId === p.id} aria-label={`${p.label}${p.scheduleLabel ? `, ${p.scheduleLabel}` : ""}`} onClick={() => { setPositionId(p.id); drawing.current = false; changed(); }}><span>{p.label}</span>{p.scheduleLabel && <small>{p.scheduleLabel}</small>}</Button>)}
    </section>
    {!position && <p className="selection-hint">Select your assigned position above to start.</p>}
    {position && <>
      <div className="selected-heading" aria-live="polite"><h2>{position.title}</h2>{position.scheduleLabel && <p>{position.scheduleLabel}</p>}</div>
      <section className="identity" aria-label="Checklist details">
        <div><label className="field-label" htmlFor="date">Date</label><Input type="date" id="date" value={date} onChange={e => { setDate(e.target.value); changed(); }} /></div>
        <div><label className="field-label" htmlFor="name">Your name</label><Input id="name" autoComplete="name" placeholder="Enter your name" maxLength={65} value={name} onChange={e => { setName(e.target.value); changed(); }} /></div>
      </section>
      {warning && <p className="source-warning" role="status">{warning}</p>}
      {position.sourceNotes && <details className="source-notes"><summary>Source instructions to confirm <ChevronDown size={18} /></summary>{position.sourceNotes.map(note => <p key={note}>{note}</p>)}</details>}
      <div key={positionId}>
        {position.sections.map((section, index) => <section className="check-section" key={section.id} aria-labelledby={`${section.id}-heading`}>
          <div className="section-heading"><span className="section-number">{index + 1}</span><h2 id={`${section.id}-heading`}>{section.title}</h2><span className="section-count">{section.items.filter(i => draft.checks[i.id]).length} / {section.items.length}</span></div>
          {section.note && <p className="source-warning">{section.note}</p>}
          {section.images?.map(img => <details className="reference" key={img.file}><summary>View {img.label} <ChevronDown size={18} /></summary><a href={`/workbook/${img.file}`} target="_blank" rel="noreferrer" aria-label={`Open full size ${img.label}`}><img src={`/workbook/${img.file}`} alt={img.label} loading="lazy" /></a></details>)}
          <div className="check-list">{section.items.map(row)}</div>
          {section.remarksKey && <div className="section-remarks"><label className="field-label" htmlFor={`remarks-${section.remarksKey}`}>{section.remarksKey === "sls1" ? "SLS1 remarks" : "Recirculation remarks"} <span className="optional">Optional</span></label><Textarea id={`remarks-${section.remarksKey}`} value={draft.sectionRemarks[section.remarksKey] ?? ""} maxLength={1000} onChange={e => updateDraft({ sectionRemarks: { ...draft.sectionRemarks, [section.remarksKey!]: e.target.value } })} /></div>}
        </section>)}
      </div>
      <div className="package-count"><label className="field-label" htmlFor="packages">{position.packageLabel}</label><div className="count-controls"><Button type="button" variant="outline" aria-label="Decrease package count" disabled={draft.count === "" || Number(draft.count) === 0} onClick={() => updateDraft({ count: String(Math.max(0, Number(draft.count) - 1)) })}><Minus /></Button><Input id="packages" type="text" inputMode="numeric" pattern="[0-9]*" placeholder="Count" value={draft.count} maxLength={5} onChange={e => updateDraft({ count: e.target.value.replace(/\D/g, "") })} /><Button type="button" variant="outline" aria-label="Increase package count" disabled={Number(draft.count) >= 99999} onClick={() => updateDraft({ count: String(Number(draft.count) + 1) })}><Plus /></Button><Button type="button" className="none-button" variant="outline" onClick={() => updateDraft({ count: "0" })}>None (0)</Button></div></div>
      <section className="finish-section" aria-labelledby="finish-up-heading"><div className="section-heading"><h2 id="finish-up-heading">Remarks and signature</h2></div>
        <label className="field-label" htmlFor="remarks">Other remarks <span className="optional">Optional</span></label><Textarea id="remarks" placeholder="Problems found, incomplete checks, actions taken or follow-up needed" value={draft.remarks} maxLength={2000} onChange={e => updateDraft({ remarks: e.target.value })} /><p className="field-hint">Explain incomplete checks here. {draft.remarks.length} / 2000 characters.</p>
        {position.handover && <div className="paper-instruction"><strong>Original paper handover instruction</strong><p>{position.handover}</p><p>Confirm the digital handover with your team leader. Downloading does not send this report.</p></div>}
        <div className="signature-heading"><h3>Signature</h3></div>
        <Tabs value={draft.signatureMode} onValueChange={v => updateDraft({ signatureMode: v as "type" | "draw" })}><TabsList className="signature-tabs" aria-label="Signature method"><TabsTrigger value="type"><Type size={18} /> Type</TabsTrigger><TabsTrigger value="draw"><PenLine size={18} /> Draw</TabsTrigger></TabsList>
          <TabsContent value="type"><label className="sr-only" htmlFor="signature">Typed signature</label><Input id="signature" className="signature-input" placeholder="Type your signature" maxLength={65} value={draft.signature} onChange={e => updateDraft({ signature: e.target.value })} /><Button type="button" variant="outline" className="use-name" disabled={!name.trim()} onClick={() => updateDraft({ signature: name })}>Use my name</Button></TabsContent>
          <TabsContent value="draw"><p className="drawing-hint">Sign in the box with your finger or mouse.</p><canvas ref={canvasRef} width="700" height="180" className="signature-canvas" aria-label="Draw your signature" onPointerDown={startDraw} onPointerMove={moveDraw} onPointerUp={endDraw} onPointerCancel={endDraw} /><Button type="button" variant="outline" className="clear-signature" onClick={() => { canvasRef.current?.getContext("2d")?.clearRect(0, 0, 700, 180); updateDraft({ drawn: "" }); }}>Clear signature</Button></TabsContent>
        </Tabs>
        <p className="field-hint">Downloading sends these entries to the hosting service to create the PDF. It does not submit them to UPS or keep a report archive. Refreshing this page clears your entries.</p>
      </section>
      <div className="status-area" role="status" aria-live="polite">{downloadStarted && <p className="success">Download requested. Check your downloads or the new tab. Your entries remain here.</p>}</div>
      <footer className="download-bar"><form className="download-inner" action="/api/download" method="POST" target="_blank" onSubmit={() => setDownloadStarted(true)}>
        <input type="hidden" name="checklist" value={JSON.stringify({ positionId, date, name, checks: draft.checks, count: draft.count, remarks: draft.remarks, sectionRemarks: draft.sectionRemarks, signature: draft.signatureMode === "type" ? draft.signature : "", drawn: draft.signatureMode === "draw" ? draft.drawn : "" })} />
        <div className="progress-copy"><strong>{completed} <span>/ {items.length}</span></strong><span>{position.label} checked</span></div><Button type="submit" className="download-button"><Download />Download PDF</Button>
      </form></footer>
    </>}
  </main>;
}
