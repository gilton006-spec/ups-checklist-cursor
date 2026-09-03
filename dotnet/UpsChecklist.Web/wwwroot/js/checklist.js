import { createEvidenceBinding } from "./evidence-binding.mjs";

(() => {
  const positions = JSON.parse(document.getElementById("positions-data").textContent);
  const state = {
    positionId: "",
    pickerOpen: true,
    name: "",
    date: "",
    drafts: {},
    downloadStarted: false,
    reportSent: "",
    photoBusy: 0,
    signatureMode: "type",
  };

  const els = {
    picker: document.getElementById("position-picker"),
    intro: document.getElementById("intro"),
    header: document.getElementById("position-header"),
    heading: document.getElementById("position-heading"),
    schedule: document.getElementById("position-schedule"),
    togglePicker: document.getElementById("toggle-picker"),
    panel: document.getElementById("checklist-panel"),
    sections: document.getElementById("sections"),
    packageLabel: document.getElementById("package-label"),
    packages: document.getElementById("packages"),
    remarks: document.getElementById("remarks"),
    name: document.getElementById("name"),
    date: document.getElementById("date"),
    signature: document.getElementById("signature"),
    useName: document.getElementById("use-name"),
    typePanel: document.getElementById("signature-type-panel"),
    drawPanel: document.getElementById("signature-draw-panel"),
    canvas: document.getElementById("signature-canvas"),
    clearSignature: document.getElementById("clear-signature"),
    downloadBar: document.getElementById("download-bar"),
    progressCount: document.getElementById("progress-count"),
    progressLabel: document.getElementById("progress-label"),
    whatsappOpen: document.getElementById("whatsapp-open"),
    statusArea: document.getElementById("status-area"),
    dialog: document.getElementById("whatsapp-dialog"),
    whatsappStatus: document.getElementById("whatsapp-status"),
    whatsappError: document.getElementById("whatsapp-error"),
    whatsappShare: document.getElementById("whatsapp-share"),
    whatsappHintShare: document.getElementById("whatsapp-hint-share"),
    whatsappHintDesktop: document.getElementById("whatsapp-hint-desktop"),
    whatsappMessage: document.getElementById("whatsapp-message"),
    whatsappClose: document.getElementById("whatsapp-close"),
    emailOpen: document.getElementById("email-open"),
    emailDialog: document.getElementById("email-dialog"),
    emailStatus: document.getElementById("email-status"),
    emailError: document.getElementById("email-error"),
    emailMessage: document.getElementById("email-message"),
    emailClose: document.getElementById("email-close"),
    evidenceRoot: document.getElementById("evidence-photo-root"),
  };

  let signaturePad = null;
  let prepared = null;
  let handoverConfig = { emailAddress: "gilton93@hotmail.com", usesTempInbox: false, devInboxUrl: null };

  async function loadHandoverConfig() {
    try {
      const response = await fetch("/api/handover-config", { cache: "no-store" });
      if (!response.ok) return;
      handoverConfig = await response.json();
      window.EmailHandover.handoverAddress = handoverConfig.emailAddress;
      const intro = document.getElementById("email-intro");
      if (intro) {
        intro.textContent = `The PDF goes to ${handoverConfig.emailAddress}. Nothing is saved on your device.`;
      }
    } catch {
      /* keep defaults */
    }
  }

  function emptyDraft() {
    return { checks: {}, count: "", remarks: "", sectionRemarks: {}, beforeSortEvidencePhoto: "", evidencePhoto: "", signature: "", drawn: "", signatureMode: "type" };
  }

  function draftFor(id) {
    if (!state.drafts[id]) state.drafts[id] = emptyDraft();
    return state.drafts[id];
  }

  function draft() {
    return draftFor(state.positionId);
  }

  const evidence = createEvidenceBinding({
    createWidget: (root, options) => window.EvidencePhoto(root, options),
    draftFor,
    onValueChange: () => changed(),
    onBusyChange(count) {
      state.photoBusy = count;
      updateActions();
    },
  });

  function position() {
    return positions.find((p) => p.id === state.positionId);
  }

  function allItems(pos) {
    return pos.sections.flatMap((s) => s.items);
  }

  function payload() {
    const d = draft();
    return JSON.stringify({
      positionId: state.positionId,
      date: state.date,
      name: state.name,
      checks: d.checks,
      count: d.count,
      remarks: d.remarks,
      sectionRemarks: d.sectionRemarks,
      beforeSortEvidencePhoto: d.beforeSortEvidencePhoto,
      evidencePhoto: d.evidencePhoto,
      signature: state.signatureMode === "type" ? d.signature : "",
      drawn: state.signatureMode === "draw" ? d.drawn : "",
    });
  }

  function changed() {
    state.downloadStarted = false;
    updateActions();
  }

  function renderPicker() {
    els.picker.innerHTML = positions.map((p) => `
      <button type="button" class="position-button ${state.positionId === p.id ? "selected" : ""}"
        data-id="${p.id}" aria-pressed="${state.positionId === p.id}">
        <span>${escapeHtml(p.label)}</span>${p.scheduleLabel ? `<small>${escapeHtml(p.scheduleLabel)}</small>` : ""}
      </button>`).join("");
    els.picker.classList.toggle("hidden", !state.pickerOpen);
  }

  function renderSections() {
    const pos = position();
    if (!pos) return;
    els.sections.innerHTML = pos.sections.map((section) => `
      <section class="check-section" aria-labelledby="${section.id}-heading">
        <div class="section-heading">
          <h2 id="${section.id}-heading">${escapeHtml(section.title)}</h2>
          <span class="section-count">${section.items.filter((i) => draft().checks[i.id]).length} / ${section.items.length}</span>
        </div>
        ${section.note ? `<p class="section-note">${escapeHtml(section.note)}</p>` : ""}
        ${(section.images || []).map((img) => `
          <figure class="diagram">
            <a href="/workbook/${img.file}" target="_blank" rel="noreferrer" aria-label="Enlarge ${escapeAttr(img.label)}">
              <img src="/workbook/${img.file}" alt="${escapeAttr(img.label)}" loading="lazy" />
              <figcaption><span>${escapeHtml(img.label)}</span><span class="enlarge-label">Enlarge</span></figcaption>
            </a>
          </figure>`).join("")}
        <div class="check-list">${section.items.map((item) => `
          <label class="check-row ${draft().checks[item.id] ? "is-checked" : ""}" for="${state.positionId}-${item.id}">
            <input type="checkbox" id="${state.positionId}-${item.id}" data-check="${item.id}" ${draft().checks[item.id] ? "checked" : ""} />
            <span class="check-copy">${escapeHtml(item.text)}${item.note ? `<span class="check-note">${escapeHtml(item.note)}</span>` : ""}</span>
          </label>`).join("")}
        </div>
        ${section.remarksKey ? `
          <div class="section-remarks">
            <label class="field-label" for="remarks-${section.remarksKey}">${section.remarksKey === "sls1" ? "SLS1 remarks" : "Recirculation remarks"} <span class="optional">Optional</span></label>
            <textarea id="remarks-${section.remarksKey}" data-remark="${section.remarksKey}" maxlength="1000">${escapeHtml(draft().sectionRemarks[section.remarksKey] || "")}</textarea>
          </div>` : ""}
        ${section.id === "before" ? `<div data-before-evidence></div>` : ""}
      </section>`).join("");
    els.packageLabel.textContent = pos.packageLabel;
    els.packages.value = draft().count;
    els.remarks.value = draft().remarks;
    els.signature.value = draft().signature;
    state.signatureMode = draft().signatureMode || "type";
    updateSignatureMode();
    mountEvidencePhotos();
    mountSignaturePad();
    bindSectionEvents();
    updateProgress();
  }

  function mountEvidencePhotos() {
    evidence.mount(state.positionId, [
      {
        root: els.sections.querySelector("[data-before-evidence]"),
        field: "beforeSortEvidencePhoto",
        label: "Before-sort evidence photo",
        hint: "Photo of the area before the sort starts. Included in your PDF. Avoid faces and parcel addresses.",
      },
      {
        root: els.evidenceRoot,
        field: "evidencePhoto",
        label: "After-sort evidence photo",
        hint: "Photo after the sort, or extra evidence. Included in your PDF. Avoid faces and parcel addresses.",
      },
    ]);
  }

  function mountSignaturePad() {
    if (!signaturePad) {
      signaturePad = window.SignaturePad(els.canvas, (value) => { draft().drawn = value; changed(); });
      els.clearSignature.addEventListener("click", () => signaturePad.clear());
    }
    signaturePad.setValue(draft().drawn);
  }

  function bindSectionEvents() {
    els.sections.querySelectorAll("[data-check]").forEach((input) => {
      input.addEventListener("change", () => {
        draft().checks[input.dataset.check] = input.checked;
        input.closest(".check-row").classList.toggle("is-checked", input.checked);
        updateProgress();
        changed();
      });
    });
    els.sections.querySelectorAll("[data-remark]").forEach((input) => {
      input.addEventListener("input", () => {
        draft().sectionRemarks[input.dataset.remark] = input.value;
        changed();
      });
    });
  }

  function updateSignatureMode() {
    els.typePanel.classList.toggle("hidden", state.signatureMode !== "type");
    els.drawPanel.classList.toggle("hidden", state.signatureMode !== "draw");
    document.querySelectorAll("[data-signature-mode]").forEach((btn) => {
      const active = btn.dataset.signatureMode === state.signatureMode;
      btn.classList.toggle("active", active);
      btn.setAttribute("aria-selected", active ? "true" : "false");
    });
    draft().signatureMode = state.signatureMode;
  }

  function updateProgress() {
    const pos = position();
    if (!pos) return;
    const items = allItems(pos);
    const completed = items.filter((i) => draft().checks[i.id]).length;
    els.progressCount.innerHTML = `${completed} <span>/ ${items.length}</span>`;
    els.progressLabel.textContent = `${pos.label} checked`;
  }

  function updateActions() {
    const disabled = state.photoBusy > 0;
    els.whatsappOpen.disabled = disabled;
    els.emailOpen.disabled = disabled;
    els.statusArea.innerHTML = state.reportSent ? `<p class="success">${escapeHtml(state.reportSent)}</p>` : "";
  }

  function renderShell() {
    const pos = position();
    els.intro.classList.toggle("hidden", !!pos && !state.pickerOpen);
    els.header.classList.toggle("hidden", !pos || state.pickerOpen);
    els.panel.classList.toggle("hidden", !pos || state.pickerOpen);
    els.downloadBar.classList.toggle("hidden", !pos || state.pickerOpen);
    if (pos) {
      els.heading.textContent = pos.label;
      els.schedule.textContent = pos.scheduleLabel || "";
      els.togglePicker.textContent = state.pickerOpen ? "Close" : "Change position";
      els.togglePicker.setAttribute("aria-expanded", state.pickerOpen ? "true" : "false");
    }
    renderPicker();
    if (pos && !state.pickerOpen) renderSections();
    updateActions();
  }

  function selectPosition(id) {
    state.positionId = id;
    state.pickerOpen = false;
    changed();
    renderShell();
    els.heading.focus({ preventScroll: true });
  }

  async function openWhatsAppDialog() {
    prepared = null;
    els.whatsappError.classList.add("hidden");
    els.whatsappMessage.classList.add("hidden");
    els.whatsappShare.classList.add("hidden");
    els.whatsappHintShare.classList.add("hidden");
    els.whatsappHintDesktop.classList.add("hidden");
    els.whatsappStatus.textContent = "Preparing your report…";
    els.dialog.showModal();
    try {
      const file = await window.WhatsAppHandover.prepareReport(
        payload(),
        window.WhatsAppHandover.reportFilename(state.positionId, state.date),
        new AbortController().signal);
      prepared = { file, canShare: window.WhatsAppHandover.supportsFileShare(file) };
      els.whatsappStatus.textContent = "";
      if (prepared.canShare) {
        els.whatsappShare.classList.remove("hidden");
        els.whatsappHintShare.classList.remove("hidden");
      } else {
        els.whatsappHintDesktop.classList.remove("hidden");
      }
    } catch {
      els.whatsappStatus.textContent = "";
      els.whatsappError.textContent = "Could not prepare the report. Your entries are still here.";
      els.whatsappError.classList.remove("hidden");
    }
  }

  async function sendEmailReport() {
    els.emailError.classList.add("hidden");
    els.emailMessage.classList.add("hidden");
    els.emailStatus.textContent = `Sending report to ${handoverConfig.emailAddress}…`;
    els.emailDialog.showModal();
    try {
      const filename = window.WhatsAppHandover.reportFilename(state.positionId, state.date);
      const result = await window.EmailHandover.sendReport(payload(), filename, new AbortController().signal);
      els.emailStatus.textContent = "";
      els.emailMessage.classList.remove("hidden");
      els.emailMessage.innerHTML = escapeHtml(result.message);
      if (result.devInboxUrl && result.usesTempInbox) {
        els.emailMessage.innerHTML += `<br><a href="${escapeAttr(result.devInboxUrl)}" target="_blank" rel="noopener noreferrer">Open inbox at Ethereal</a>`;
      }
      state.reportSent = result.message;
      updateActions();
    } catch (error) {
      els.emailStatus.textContent = "";
      els.emailError.innerHTML = escapeHtml(error instanceof Error ? error.message : "The report could not be sent.").replaceAll("\n", "<br>");
      els.emailError.classList.remove("hidden");
    }
  }

  function escapeHtml(value) {
    return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");
  }

  function escapeAttr(value) {
    return escapeHtml(value);
  }

  els.picker.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-id]");
    if (btn) selectPosition(btn.dataset.id);
  });
  els.togglePicker.addEventListener("click", () => { state.pickerOpen = !state.pickerOpen; renderShell(); });
  els.name.addEventListener("input", () => { state.name = els.name.value; els.useName.disabled = !state.name.trim(); changed(); });
  els.date.addEventListener("input", () => { state.date = els.date.value; changed(); });
  els.packages.addEventListener("input", () => { draft().count = els.packages.value.replace(/\D/g, ""); els.packages.value = draft().count; changed(); });
  els.remarks.addEventListener("input", () => { draft().remarks = els.remarks.value; changed(); });
  els.signature.addEventListener("input", () => { draft().signature = els.signature.value; changed(); });
  els.useName.addEventListener("click", () => { draft().signature = state.name; els.signature.value = draft().signature; changed(); });
  document.querySelectorAll("[data-signature-mode]").forEach((btn) => btn.addEventListener("click", () => { state.signatureMode = btn.dataset.signatureMode; updateSignatureMode(); changed(); }));
  document.getElementById("count-dec").addEventListener("click", () => { draft().count = String(Math.max(0, Number(draft().count || 0) - 1)); els.packages.value = draft().count; changed(); });
  document.getElementById("count-inc").addEventListener("click", () => { draft().count = String(Number(draft().count || 0) + 1); els.packages.value = draft().count; changed(); });
  document.getElementById("count-none").addEventListener("click", () => { draft().count = "0"; els.packages.value = "0"; changed(); });
  els.whatsappOpen.addEventListener("click", openWhatsAppDialog);
  els.emailOpen.addEventListener("click", sendEmailReport);
  els.whatsappShare.addEventListener("click", async () => {
    if (!prepared) return;
    const result = await window.WhatsAppHandover.shareReport(prepared.file);
    els.whatsappMessage.classList.remove("hidden");
    els.whatsappMessage.textContent = result === "cancelled"
      ? "Sharing cancelled. Nothing was confirmed sent."
      : result === "returned"
        ? `Check WhatsApp, confirm ${window.WhatsAppHandover.handoverNumber}, then send.`
        : "Could not share. Use Send report instead.";
  });
  els.whatsappClose.addEventListener("click", () => els.dialog.close());
  els.emailClose.addEventListener("click", () => els.emailDialog.close());

  const today = new Date();
  state.date = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
  els.date.value = state.date;
  window.addEventListener("beforeunload", (event) => {
    const hasEntries = state.name.trim() || Object.values(state.drafts).some((d) =>
      Object.values(d.checks).some(Boolean) || d.count || d.remarks || d.beforeSortEvidencePhoto || d.evidencePhoto || d.signature || d.drawn || Object.values(d.sectionRemarks).some(Boolean));
    if (!hasEntries) return;
    event.preventDefault();
    event.returnValue = "";
  });
  loadHandoverConfig();
  renderShell();
})();
