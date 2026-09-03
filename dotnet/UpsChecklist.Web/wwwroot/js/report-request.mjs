export class ReportRequestError extends Error {
  constructor(message, { timeout = false, aborted = false } = {}) {
    super(message);
    this.timeout = timeout;
    this.aborted = aborted;
  }
}

export async function fetchWithTimeout(url, options = {}, timeoutMs = 45000) {
  const userSignal = options.signal;
  if (userSignal?.aborted) {
    throw new ReportRequestError("The request was cancelled. Your entries are still here.", { aborted: true });
  }

  const timeout = new AbortController();
  const timer = setTimeout(() => timeout.abort(), timeoutMs);
  const onAbort = () => timeout.abort();
  userSignal?.addEventListener("abort", onAbort);
  try {
    return await fetch(url, { ...options, signal: timeout.signal, cache: options.cache ?? "no-store" });
  } catch (error) {
    if (userSignal?.aborted) {
      throw new ReportRequestError("The request was cancelled. Your entries are still here.", { aborted: true });
    }
    if (error instanceof ReportRequestError) throw error;
    throw new ReportRequestError(
      "The request timed out or could not be completed. Your entries are still here. Whether the server finished is unknown.",
      { timeout: true });
  } finally {
    clearTimeout(timer);
    userSignal?.removeEventListener("abort", onAbort);
  }
}

export function antiforgeryHeaders(documentRef = document) {
  const token = documentRef.querySelector('input[name="__RequestVerificationToken"]')?.value;
  return token ? { RequestVerificationToken: token } : {};
}
