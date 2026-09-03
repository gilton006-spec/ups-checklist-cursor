window.WhatsAppHandover = {
  handoverNumber: "+31 626149058",
  handoverPhoneDigits: "31626149058",
  handoverMessage: "Sunrise checklist. I will attach the report PDF here.",
  get handoverChatUrl() {
    return `https://wa.me/${this.handoverPhoneDigits}?text=${encodeURIComponent(this.handoverMessage)}`;
  },
  reportFilename(positionId, date) {
    const safeId = positionId.replace(/[^a-z0-9-]/gi, "");
    const safeDate = date.replace(/[^0-9-]/g, "");
    return `UPS_${safeId}_${safeDate || "undated"}.pdf`;
  },
  isMobile() {
    return /Android|iPhone|iPad|iPod/i.test(navigator.userAgent);
  },
  openHandoverChat() {
    const text = encodeURIComponent(this.handoverMessage);
    const phone = this.handoverPhoneDigits;
    const webUrl = `https://wa.me/${phone}?text=${text}`;
    if (this.isMobile()) {
      const appUrl = `whatsapp://send?phone=${phone}&text=${text}`;
      window.location.href = appUrl;
      window.setTimeout(() => {
        window.open(webUrl, "_blank", "noopener,noreferrer");
      }, 1200);
      return;
    }
    window.open(webUrl, "_blank", "noopener,noreferrer");
  },
  downloadReport(file, objectUrl) {
    const link = document.createElement("a");
    link.href = objectUrl;
    link.download = file.name;
    link.rel = "noopener";
    document.body.appendChild(link);
    link.click();
    link.remove();
  },
  supportsFileShare(file) {
    try {
      return typeof navigator.share === "function" && typeof navigator.canShare === "function" && navigator.canShare({ files: [file] });
    } catch {
      return false;
    }
  },
  async shareReport(file) {
    if (!this.supportsFileShare(file)) return "unsupported";
    try {
      await navigator.share({
        files: [file],
        title: "Sunrise checklist",
        text: `Send to ${this.handoverNumber}. Confirm the recipient in WhatsApp before sending.`,
      });
      return "returned";
    } catch (error) {
      return error instanceof Error && error.name === "AbortError" ? "cancelled" : "failed";
    }
  },
  sendViaWhatsApp(file, objectUrl) {
    this.downloadReport(file, objectUrl);
    this.openHandoverChat();
    return "download-and-chat";
  },
  async prepareReport(payload, filename, signal, headers = {}) {
    const form = new FormData();
    form.set("checklist", payload);
    const response = await fetch("/api/download", {
      method: "POST",
      body: form,
      signal,
      cache: "no-store",
      headers,
    });
    if (!response.ok || !response.headers.get("content-type")?.startsWith("application/pdf")) {
      throw new Error("The PDF could not be prepared. Your entries are still here. Try again.");
    }
    return new File([await response.blob()], filename, { type: "application/pdf" });
  },
};
