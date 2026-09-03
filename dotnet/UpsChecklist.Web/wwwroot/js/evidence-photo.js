window.EvidencePhoto = function EvidencePhoto(root, { onChange, onBusyChange, label, hint }) {
  const title = label || "Evidence photo";
  root.innerHTML = `
    <section class="evidence-photo" aria-label="${title}">
      <p class="field-label">${title} <span class="optional">Optional</span></p>
      <div class="photo-actions">
        <button type="button" class="btn secondary" data-action="camera">Take photo</button>
        <button type="button" class="btn secondary" data-action="gallery">Choose photo</button>
      </div>
      <input type="file" accept="image/*" capture="environment" hidden data-input="camera" />
      <input type="file" accept="image/*" hidden data-input="gallery" />
      <p class="hidden" role="status" data-status></p>
      <p class="photo-error hidden" role="alert" data-error></p>
      <div class="photo-preview hidden" data-preview>
        <img alt="${title} preview" data-img />
        <button type="button" class="btn secondary" data-remove>Remove photo</button>
      </div>
      <p class="field-hint">${hint || "Included in your PDF. Avoid faces and parcel addresses."}</p>
    </section>`;

  let value = "";
  let busy = false;
  let request = 0;
  const camera = root.querySelector('[data-input="camera"]');
  const gallery = root.querySelector('[data-input="gallery"]');
  const status = root.querySelector("[data-status]");
  const error = root.querySelector("[data-error]");
  const preview = root.querySelector("[data-preview]");
  const img = root.querySelector("[data-img]");

  function setBusy(next) {
    busy = next;
    onBusyChange?.(busy);
    root.querySelectorAll("button").forEach((btn) => (btn.disabled = busy));
    status.classList.toggle("hidden", !busy);
    status.textContent = busy ? "Adding photo…" : "";
  }

  async function add(file) {
    if (!file) return;
    error.classList.add("hidden");
    if (!file.type.startsWith("image/") || file.type === "image/svg+xml") {
      error.textContent = "Choose a photo, such as a JPEG or PNG.";
      error.classList.remove("hidden");
      return;
    }
    if (file.size > 15 * 1024 * 1024) {
      error.textContent = "Choose a photo smaller than 15 MB.";
      error.classList.remove("hidden");
      return;
    }
    const current = ++request;
    setBusy(true);
    const url = URL.createObjectURL(file);
    try {
      const image = new Image();
      await new Promise((resolve, reject) => {
        image.onload = () => resolve();
        image.onerror = reject;
        image.src = url;
      });
      if (request !== current) return;
      const scale = Math.min(1, 1280 / Math.max(image.naturalWidth, image.naturalHeight));
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
      canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
      const ctx = canvas.getContext("2d");
      ctx.fillStyle = "#fff";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
      let photo = canvas.toDataURL("image/jpeg", 0.78);
      if (photo.length > 800000) photo = canvas.toDataURL("image/jpeg", 0.55);
      if (photo.length > 800000 || !photo.startsWith("data:image/jpeg;base64,")) throw new Error("too large");
      value = photo;
      img.src = photo;
      preview.classList.remove("hidden");
      onChange?.(value);
    } catch {
      if (request === current) {
        error.textContent = "Could not add this photo. Try a JPEG or PNG.";
        error.classList.remove("hidden");
      }
    } finally {
      URL.revokeObjectURL(url);
      if (request === current) setBusy(false);
    }
  }

  root.querySelector('[data-action="camera"]').addEventListener("click", () => camera.click());
  root.querySelector('[data-action="gallery"]').addEventListener("click", () => gallery.click());
  camera.addEventListener("change", (e) => { add(e.target.files?.[0]); e.target.value = ""; });
  gallery.addEventListener("change", (e) => { add(e.target.files?.[0]); e.target.value = ""; });
  root.querySelector("[data-remove]").addEventListener("click", () => {
    value = "";
    preview.classList.add("hidden");
    onChange?.("");
  });

  return {
    getValue: () => value,
    setValue(next) {
      value = next || "";
      if (value) {
        img.src = value;
        preview.classList.remove("hidden");
      } else preview.classList.add("hidden");
    },
    reset() {
      value = "";
      preview.classList.add("hidden");
      error.classList.add("hidden");
    },
    // Drop a photo that is still loading, so a replaced widget reports nothing further.
    cancel() {
      request += 1;
      if (busy) setBusy(false);
    },
  };
};
