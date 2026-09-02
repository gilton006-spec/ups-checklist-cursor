"use client";

import { useEffect, useRef, useState, type PointerEvent } from "react";
import { Check, Download, Minus, Plus, Sun, PenLine, Type, ChevronDown } from "lucide-react";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";

const before = [
  { id: "tape_for_repacking", text: "Take some tape with you for repacking" },
  { id: "on_time", text: "Be on time upstairs and on position" },
  { id: "clean_at_start", text: "Singulator position clean", note: "When you start" },
];
const after = [
  { id: "check_belts", text: "Check all belts" },
  { id: "check_chutes", text: "Check all chutes" },
  { id: "check_work_floor", text: "Check also on the work floor, the first part of the metro belt" },
  { id: "return_tape", text: "Bring the tape back to the Control Room" },
  { id: "clean_at_finish", text: "Singulator position clean", note: "When you finish" },
];
type Item = { id: string; text: string; note?: string };

export default function Home() {
  const [date, setDate] = useState("");
  const [name, setName] = useState("");
  const [checks, setChecks] = useState<Record<string, boolean>>({});
  const [count, setCount] = useState("");
  const [remarks, setRemarks] = useState("");
  const [signatureMode, setSignatureMode] = useState("type");
  const [signature, setSignature] = useState("");
  const [drawn, setDrawn] = useState("");
  const [downloadStarted, setDownloadStarted] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const completed = Object.values(checks).filter(Boolean).length;

  useEffect(() => {
    const d = new Date();
    setDate(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`);
  }, []);

  function changed() { setDownloadStarted(false); }
  function point(e: PointerEvent<HTMLCanvasElement>) {
    const r = e.currentTarget.getBoundingClientRect();
    return [(e.clientX - r.left) * 700 / r.width, (e.clientY - r.top) * 180 / r.height];
  }
  function startDraw(e: PointerEvent<HTMLCanvasElement>) {
    e.preventDefault(); changed(); drawing.current = true; e.currentTarget.setPointerCapture(e.pointerId);
    const ctx = e.currentTarget.getContext("2d")!;
    const [x, y] = point(e); ctx.strokeStyle = "#20170f"; ctx.fillStyle = "#20170f"; ctx.lineWidth = 2.8; ctx.lineCap = "round"; ctx.lineJoin = "round";
    ctx.beginPath(); ctx.arc(x,y,1.4,0,Math.PI*2); ctx.fill(); ctx.beginPath(); ctx.moveTo(x,y);
  }
  function moveDraw(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return; e.preventDefault(); const ctx = e.currentTarget.getContext("2d")!;
    const [x, y] = point(e); ctx.lineTo(x,y); ctx.stroke();
  }
  function endDraw(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return; drawing.current = false;
    setDrawn(e.currentTarget.toDataURL("image/png"));
  }
  function clearDraw() { canvasRef.current?.getContext("2d")?.clearRect(0,0,700,180); setDrawn(""); changed(); }
  function restoreDraw() {
    if (drawn && canvasRef.current) { const img = new Image(); img.onload = () => canvasRef.current?.getContext("2d")?.drawImage(img,0,0); img.src = drawn; }
  }

  function row(item: Item) {
    return <label htmlFor={item.id} className={`check-row ${checks[item.id] ? "is-checked" : ""}`} key={item.id}>
      <Checkbox id={item.id} checked={!!checks[item.id]} onCheckedChange={value=>{setChecks(previous=>({...previous,[item.id]:value===true}));changed();}} className="check-box" />
      <span className="check-copy">{item.text}{item.note && <span className="check-note">{item.note}</span>}</span>
      {checks[item.id] && <span className="done-label" aria-hidden="true">Done</span>}
    </label>;
  }

  return <main className="page-shell">
    <header className="topbar"><div className="wordmark"><img src="/ups-logo.jpg" alt="UPS" width="47" height="47"/><span>PD4 <span className="wordmark-light">Checklist</span></span></div><span className="shift"><Sun size={17}/> Sunrise</span></header>
    <div className="intro"><p className="eyebrow">CHECKLIST JAMB. / SING.</p><h1>Jambreaker / Singulator</h1><p>Tap each item as you complete it.</p></div>
    <section className="identity" aria-label="Checklist details">
      <div><label className="field-label" htmlFor="date">Date</label><Input type="date" id="date" value={date} onChange={e=>{setDate(e.target.value);changed();}} /></div>
      <div><label className="field-label" htmlFor="name">Your name</label><Input id="name" autoComplete="name" placeholder="Enter your name" maxLength={65} value={name} onChange={e=>{setName(e.target.value);changed();}} /></div>
    </section>
    <section className="check-section" aria-labelledby="before-heading"><div className="section-heading"><span className="section-number">1</span><h2 id="before-heading">Before the sort</h2><span className="section-count">{before.filter(i=>checks[i.id]).length} / 3</span></div><div className="check-list">{before.map(row)}</div></section>
    <section className="check-section" aria-labelledby="after-heading"><div className="section-heading"><span className="section-number">2</span><h2 id="after-heading">After the sort</h2><span className="section-count">{after.filter(i=>checks[i.id]).length} / 5</span></div>
      <details className="reference"><summary>View conveyor diagram <ChevronDown size={18}/></summary><a href="/conveyor.jpg" target="_blank" rel="noreferrer" aria-label="Open larger conveyor diagram"><img src="/conveyor.jpg" alt="Original PD4 conveyor and chute layout" width="1071" height="287"/></a></details>
      <div className="check-list">{after.slice(0,3).map(row)}</div>
      <details className="reference floor-reference"><summary>View metro belt photo <ChevronDown size={18}/></summary><img src="/metro-belt.jpg" alt="First part of the metro belt to inspect on the work floor" width="409" height="269"/></details>
      <div className="package-count"><label className="field-label" htmlFor="packages">How many packages did you find?</label><div className="count-controls"><Button variant="outline" aria-label="Decrease package count" disabled={count==="" || Number(count)===0} onClick={()=>{setCount(String(Math.max(0,Number(count)-1)));changed();}}><Minus/></Button><Input id="packages" type="text" inputMode="numeric" pattern="[0-9]*" placeholder="0" value={count} maxLength={5} onChange={e=>{setCount(e.target.value.replace(/\D/g,""));changed();}}/><Button variant="outline" aria-label="Increase package count" disabled={Number(count)>=99999} onClick={()=>{setCount(String(Number(count)+1));changed();}}><Plus/></Button><Button className="none-button" variant="outline" onClick={()=>{setCount("0");changed();}}>None (0)</Button></div></div>
      <div className="check-list bottom-checks">{after.slice(3).map(row)}</div>
    </section>
    <section className="finish-section" aria-labelledby="finish-heading"><div className="section-heading"><span className="section-number">3</span><h2 id="finish-heading">Finish up</h2></div>
      <label className="field-label" htmlFor="remarks">Other remarks <span className="optional">Optional</span></label><Textarea id="remarks" placeholder="Anything else to mention?" value={remarks} maxLength={300} onChange={e=>{setRemarks(e.target.value);changed();}}/>
      <div className="signature-heading"><h3>Signature</h3></div>
      <Tabs value={signatureMode} onValueChange={v=>{setSignatureMode(v);changed();}}><TabsList className="signature-tabs" aria-label="Signature method"><TabsTrigger value="type"><Type size={18}/> Type</TabsTrigger><TabsTrigger value="draw"><PenLine size={18}/> Draw</TabsTrigger></TabsList>
        <TabsContent value="type"><label className="sr-only" htmlFor="signature">Typed signature</label><Input id="signature" className="signature-input" placeholder="Type your signature" maxLength={65} value={signature} onChange={e=>{setSignature(e.target.value);changed();}}/><Button variant="outline" className="use-name" disabled={!name.trim()} onClick={()=>{setSignature(name);changed();}}>Use my name</Button></TabsContent>
        <TabsContent value="draw"><p className="drawing-hint">Sign in the box with your finger or mouse.</p><canvas ref={el=>{canvasRef.current=el; if(el) restoreDraw();}} width="700" height="180" className="signature-canvas" aria-label="Draw your signature" onPointerDown={startDraw} onPointerMove={moveDraw} onPointerUp={endDraw} onPointerCancel={endDraw}/><Button variant="outline" className="clear-signature" onClick={clearDraw}>Clear signature</Button></TabsContent>
      </Tabs>
    </section>
    <div className="status-area" role="status" aria-live="polite">{downloadStarted && <p className="success"><Check size={18}/> Your PDF is downloading.</p>}</div>
    <footer className="download-bar"><form className="download-inner" action="/api/download" method="POST" onSubmit={()=>setDownloadStarted(true)}>
      <input type="hidden" name="checklist" value={JSON.stringify({date,name,checks,count,remarks,signature:signatureMode === "type" ? signature : "",drawn:signatureMode === "draw" ? drawn : ""})}/>
      <div className="progress-copy"><strong>{completed} <span>/ 8</span></strong><span>items checked</span></div><Button type="submit" className="download-button"><Download/>Download PDF</Button>
    </form></footer>
  </main>;
}
