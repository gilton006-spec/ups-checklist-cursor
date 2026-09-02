export const handoverNumber = "+31 626149058";
export const handoverChatUrl = "https://wa.me/31626149058?text=" + encodeURIComponent("Sunrise checklist. I will attach the report PDF here.");

export function reportFilename(positionId: string, date: string) {
  return `UPS_${positionId.replace(/[^a-z0-9-]/gi, "")}_${date.replace(/[^0-9-]/g, "") || "undated"}.pdf`;
}

export async function prepareReport(payload: string, filename: string, signal: AbortSignal): Promise<File> {
  const form = new FormData();
  form.set("checklist", payload);
  const response = await fetch("/api/download", { method: "POST", body: form, signal, cache: "no-store" });
  if (!response.ok || !response.headers.get("content-type")?.startsWith("application/pdf")) {
    throw new Error("The PDF could not be prepared. Your entries are still here. Try again.");
  }
  return new File([await response.blob()], filename, { type: "application/pdf" });
}

export function supportsFileShare(file: File, nav: Pick<Navigator, "share" | "canShare">): boolean {
  try { return typeof nav.share === "function" && typeof nav.canShare === "function" && nav.canShare({ files: [file] }); }
  catch { return false; }
}

export async function shareReport(file: File, nav: Pick<Navigator, "share" | "canShare">): Promise<"returned" | "cancelled" | "unsupported" | "failed"> {
  if (!supportsFileShare(file, nav)) return "unsupported";
  try {
    // Called from a fresh button gesture. The OS, not this site, chooses app and recipient.
    await nav.share({ files: [file], title: "Sunrise checklist" });
    return "returned";
  } catch (error) {
    return error instanceof Error && error.name === "AbortError" ? "cancelled" : "failed";
  }
}
