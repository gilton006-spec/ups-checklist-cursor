import { createImageRestore } from "./signature-restore.mjs";

window.SignaturePad = function SignaturePad(canvas, onChange) {
  const drawing = { active: false };
  let value = "";
  const restore = createImageRestore((image) => {
    canvas.getContext("2d").drawImage(image, 0, 0);
  });

  function point(e) {
    const r = canvas.getBoundingClientRect();
    return [(e.clientX - r.left) * 700 / r.width, (e.clientY - r.top) * 180 / r.height];
  }

  function start(e) {
    e.preventDefault();
    drawing.active = true;
    canvas.setPointerCapture(e.pointerId);
    const ctx = canvas.getContext("2d");
    const [x, y] = point(e);
    ctx.strokeStyle = "#20170f";
    ctx.fillStyle = "#20170f";
    ctx.lineWidth = 2.8;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.beginPath();
    ctx.arc(x, y, 1.4, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(x, y);
  }

  function move(e) {
    if (!drawing.active) return;
    e.preventDefault();
    const ctx = canvas.getContext("2d");
    const [x, y] = point(e);
    ctx.lineTo(x, y);
    ctx.stroke();
  }

  function end(e) {
    if (!drawing.active) return;
    drawing.active = false;
    value = canvas.toDataURL("image/png");
    onChange?.(value);
  }

  canvas.addEventListener("pointerdown", start);
  canvas.addEventListener("pointermove", move);
  canvas.addEventListener("pointerup", end);
  canvas.addEventListener("pointercancel", end);

  return {
    clear() {
      value = "";
      canvas.getContext("2d").clearRect(0, 0, canvas.width, canvas.height);
      restore("");
      onChange?.("");
    },
    setValue(next) {
      value = next || "";
      const ctx = canvas.getContext("2d");
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      restore(value);
    },
    getValue: () => value,
  };
};
