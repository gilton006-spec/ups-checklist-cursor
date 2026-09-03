window.EmailHandover = {
  handoverAddress: "gilton93@hotmail.com",
  async sendReport(payload, filename, signal, headers = {}) {
    const { fetchWithTimeout } = await import("./report-request.mjs");
    const form = new FormData();
    form.set("checklist", payload);
    const response = await fetchWithTimeout("/api/email-handover", {
      method: "POST",
      body: form,
      signal,
      headers,
    });
    const contentType = response.headers.get("content-type") ?? "";
    if (!response.ok) {
      if (response.status === 503 && contentType.includes("json")) {
        const body = await response.json();
        const steps = Array.isArray(body.setup) ? body.setup.join("\n") : "";
        throw new Error([body.message, steps].filter(Boolean).join("\n\n"));
      }
      throw new Error(contentType.includes("json")
        ? (await response.json()).message
        : await response.text() || "The report could not be sent.");
    }
    if (!contentType.includes("json"))
      throw new Error("Unexpected response from the server.");
    return response.json();
  },
};
