"use client";

import { useEffect, useRef, useState, type PointerEvent } from "react";
import { Download, Minus, Plus, Sun, PenLine, Type, ChevronDown, Expand } from "lucide-react";
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
  const [pickerOpen, setPickerOpen] = useState(true);
  const [drafts, setDrafts] = useState<Record<string, PositionDraft>>({});
  const [downloadStarted, setDownloadStarted] = useState(false);
  const positionHeadingRef = useRef<HTMLHeadingElement>(null);
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
    if (positionId && !pickerOpen) positionHeadingRef.current?.focus({ preventScroll: true });
  }, [positionId, pickerOpen]);

  useEffect(() => {
    const hasEntries = name.trim() || Object.values(drafts).some(d => Object.values(d.checks).some(Boolean) || d.count || d.remarks || d.signature || d.drawn || Object.values(d.sectionRemarks).some(Boolean));
    if (!hasEntries) return;
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", warnBeforeLeaving);
    return () => window.removeEventListener("beforeunload", warnBeforeLeaving);
  }, [name, drafts]);

  function changed() { setDownloadStarted(false); }
  function updateDraft(update: Partial<PositionDraft>) {
    setDrafts(previous => ({ ...previous, [positionId]: { ...(previous[positionId] ?? emptyDraft()), ...update } }));
    changed();
  }
  function row(item: CheckItem) {
    const id = `${positionId}-${item.id}`;
    return <label htmlFor={id} className={`check-row ${draft.checks[item.id] ? "is-checked" : ""}`} key={item.id}>
      <Checkbox id={id} checked={!!draft.checks[item.id]} onCheckedChange={value => updateDraft({ checks: { ...draft.checks, [item.id]: value === true } })} className="check-box" />
      <span className="check-copy">{item.text}{item.note && <span className="check-note">{item.note}</span>}</span>
    </label>;
  }

  return <main className="page-shell">
    <header className="topbar"><div className="wordmark"><img src="/workbook/image1.png" alt="UPS" width="47" height="47" /><span>Shift <span className="wordmark-light">Checklist</span></span></div><span className="shift"><Sun size={17} /> Sunrise</span></header>
    <p className="demo-label">Demo only. Not UPS approved.</p>
    {position ? <div className="position-header"><div><h1 ref={positionHeadingRef} tabIndex={-1}>{position.label}</h1>{position.scheduleLabel && <p>{position.scheduleLabel}</p>}</div><Button type="button" variant="outline" aria-expanded={pickerOpen} aria-controls="position-picker" onClick={() => setPickerOpen(open => !open)}>{pickerOpen ? "Close" : "Change position"}</Button></div> : <div className="intro"><h1>Choose your position</h1></div>}
    {pickerOpen && <section id="position-picker" className="position-grid" aria-label="Jambreaker position">
      {positions.map(p => <Button key={p.id} type="button" variant="outline" className={`position-button ${positionId === p.id ? "selected" : ""}`} aria-pressed={positionId === p.id} aria-label={`${p.label}${p.scheduleLabel ? `, ${p.scheduleLabel}` : ""}`} onClick={() => { setPositionId(p.id); setPickerOpen(false); changed(); }}><span>{p.label}</span>{p.scheduleLabel && <small>{p.scheduleLabel}</small>}</Button>)}
    </section>}
    {position && <>
      <section className="identity" aria-label="Checklist details">
        <div><label className="field-label" htmlFor="date">Date</label><Input type="date" id="date" value={date} onChange={e => { setDate(e.target.value); changed(); }} /></div>
        <div><label className="field-label" htmlFor="name">Name</label><Input id="name" autoComplete="name" maxLength={65} value={name} onChange={e => { setName(e.target.value); changed(); }} /></div>
      </section>
      {warning && <p className="source-warning" role="status">{warning}</p>}
      {position.sourceNotes && <details className="source-notes"><summary>Confirm with your team leader <ChevronDown size={18} /></summary>{position.sourceNotes.map(note => <p key={note}>{note}</p>)}</details>}
      <div key={positionId}>
        {position.sections.map(section => <section className="check-section" key={section.id} aria-labelledby={`${section.id}-heading`}>
          <div className="section-heading"><h2 id={`${section.id}-heading`}>{section.title}</h2><span className="section-count">{section.items.filter(i => draft.checks[i.id]).length} / {section.items.length}</span></div>
          {section.note && <p className="source-warning">{section.note}</p>}
          {section.images?.map(img => <figure className="diagram" key={img.file}><a href={`/workbook/${img.file}`} target="_blank" rel="noreferrer" aria-label={`Enlarge ${img.label}`}><img src={`/workbook/${img.file}`} alt={img.label} loading="lazy" /><figcaption><span>{img.label}</span><span className="enlarge-label"><Expand size={16} /> Enlarge</span></figcaption></a></figure>)}
          <div className="check-list">{section.items.map(row)}</div>
          {section.remarksKey && <div className="section-remarks"><label className="field-label" htmlFor={`remarks-${section.remarksKey}`}>{section.remarksKey === "sls1" ? "SLS1 remarks" : "Recirculation remarks"} <span className="optional">Optional</span></label><Textarea id={`remarks-${section.remarksKey}`} value={draft.sectionRemarks[section.remarksKey] ?? ""} maxLength={1000} onChange={e => updateDraft({ sectionRemarks: { ...draft.sectionRemarks, [section.remarksKey!]: e.target.value } })} /></div>}
        </section>)}
      </div>
      <div className="package-count"><label className="field-label" htmlFor="packages">{position.packageLabel}</label><div className="count-controls"><Button type="button" variant="outline" aria-label="Decrease package count" disabled={draft.count === "" || Number(draft.count) === 0} onClick={() => updateDraft({ count: String(Math.max(0, Number(draft.count) - 1)) })}><Minus /></Button><Input id="packages" type="text" inputMode="numeric" pattern="[0-9]*" placeholder="Count" value={draft.count} maxLength={5} onChange={e => updateDraft({ count: e.target.value.replace(/\D/g, "") })} /><Button type="button" variant="outline" aria-label="Increase package count" disabled={Number(draft.count) >= 99999} onClick={() => updateDraft({ count: String(Number(draft.count) + 1) })}><Plus /></Button><Button type="button" className="none-button" variant="outline" onClick={() => updateDraft({ count: "0" })}>None (0)</Button></div></div>
      <section className="finish-section" aria-label="Remarks and signature">
        <label className="field-label" htmlFor="remarks">Remarks <span className="optional">Optional</span></label><Textarea id="remarks" placeholder="Issues, unfinished checks or follow-up" value={draft.remarks} maxLength={2000} onChange={e => updateDraft({ remarks: e.target.value })} />
        {position.handover && <details className="source-notes"><summary>Paper handover instructions <ChevronDown size={18} /></summary><p>{position.handover}</p><p>Confirm the digital handover with your team leader.</p></details>}
        <div className="signature-heading"><h3>Signature</h3></div>
        <Tabs value={draft.signatureMode} onValueChange={v => updateDraft({ signatureMode: v as "type" | "draw" })}><TabsList className="signature-tabs" aria-label="Signature method"><TabsTrigger value="type"><Type size={18} /> Type name</TabsTrigger><TabsTrigger value="draw"><PenLine size={18} /> Draw signature</TabsTrigger></TabsList>
          <TabsContent value="type"><label className="sr-only" htmlFor="signature">Typed signature</label><Input id="signature" className="signature-input" placeholder="Type your signature" maxLength={65} value={draft.signature} onChange={e => updateDraft({ signature: e.target.value })} /><Button type="button" variant="outline" className="use-name" disabled={!name.trim()} onClick={() => updateDraft({ signature: name })}>Use my name</Button></TabsContent>
          <TabsContent value="draw"><SignaturePad key={positionId} value={draft.drawn} onChange={drawn => updateDraft({ drawn })} /></TabsContent>
        </Tabs>
        <p className="field-hint">PDF uses external hosting. Not sent to UPS. No report archive. Refresh clears entries.</p>
      </section>
      <div className="status-area" role="status" aria-live="polite">{downloadStarted && <p className="success">Check your downloads or the new tab.</p>}</div>
      <footer className="download-bar"><form className="download-inner" action="/api/download" method="POST" target="_blank" onSubmit={() => setDownloadStarted(true)}>
        <input type="hidden" name="checklist" value={JSON.stringify({ positionId, date, name, checks: draft.checks, count: draft.count, remarks: draft.remarks, sectionRemarks: draft.sectionRemarks, signature: draft.signatureMode === "type" ? draft.signature : "", drawn: draft.signatureMode === "draw" ? draft.drawn : "" })} />
        <div className="progress-copy"><strong>{completed} <span>/ {items.length}</span></strong><span>{position.label} checked</span></div><Button type="submit" className="download-button"><Download />Download PDF</Button>
      </form></footer>
    </>}
  </main>;
}

function SignaturePad({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const renderedValue = useRef("");

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || renderedValue.current === value) return;
    renderedValue.current = value;
    const ctx = canvas.getContext("2d");
    ctx?.clearRect(0, 0, canvas.width, canvas.height);
    if (!value) return;
    const img = new Image();
    let active = true;
    img.onload = () => { if (active) ctx?.drawImage(img, 0, 0); };
    img.src = value;
    return () => { active = false; renderedValue.current = ""; };
  }, [value]);

  function point(e: PointerEvent<HTMLCanvasElement>) {
    const r = e.currentTarget.getBoundingClientRect();
    return [(e.clientX - r.left) * 700 / r.width, (e.clientY - r.top) * 180 / r.height];
  }
  function start(e: PointerEvent<HTMLCanvasElement>) {
    e.preventDefault(); drawing.current = true; e.currentTarget.setPointerCapture(e.pointerId);
    const ctx = e.currentTarget.getContext("2d")!;
    const [x, y] = point(e);
    ctx.strokeStyle = "#20170f"; ctx.fillStyle = "#20170f"; ctx.lineWidth = 2.8; ctx.lineCap = "round"; ctx.lineJoin = "round";
    ctx.beginPath(); ctx.arc(x, y, 1.4, 0, Math.PI * 2); ctx.fill(); ctx.beginPath(); ctx.moveTo(x, y);
  }
  function move(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return;
    e.preventDefault(); const ctx = e.currentTarget.getContext("2d")!;
    const [x, y] = point(e); ctx.lineTo(x, y); ctx.stroke();
  }
  function end(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return;
    drawing.current = false;
    renderedValue.current = e.currentTarget.toDataURL("image/png");
    onChange(renderedValue.current);
  }
  return <><p className="drawing-hint">Sign here</p><canvas ref={canvasRef} width="700" height="180" className="signature-canvas" aria-label="Draw your signature" onPointerDown={start} onPointerMove={move} onPointerUp={end} onPointerCancel={end} /><Button type="button" variant="outline" className="clear-signature" onClick={() => onChange("")}>Clear</Button></>;
}
