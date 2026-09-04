/**
 * Refuse mutating E2E runs against non-local hosts (Fly.io / production).
 */
export function assertLocalBaseUrl(
  url: string | undefined,
  options: { allowUnset?: boolean } = {},
): void {
  if (!url) {
    if (options.allowUnset) return;
    throw new Error('PLAYWRIGHT_BASE_URL is required.');
  }

  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`PLAYWRIGHT_BASE_URL is not a valid URL: ${url}`);
  }

  const host = parsed.hostname.toLowerCase();
  const local = host === 'localhost' || host === '127.0.0.1' || host === '::1';
  if (local) return;

  if (host.includes('fly.dev') || host.includes('fly.io') || !local) {
    throw new Error(
      `Refusing to run checklist E2E against non-local host "${host}". ` +
        'Only localhost / 127.0.0.1 are allowed for tests that submit data.',
    );
  }
}

export const EXTERNAL_SMTP_HOST_RE =
  /smtp\.(ethereal\.email|office365\.com|outlook\.com|gmail\.com|hotmail\.com)|api\.nodemailer\.com/i;

export const EXTERNAL_HANDOVER_HOST_RE =
  /(?:^|\.)wa\.me$|(?:^|\.)whatsapp\.com$|(?:^|\.)api\.whatsapp\.com$/i;

export function assertNoExternalMailHost(url: string): void {
  if (EXTERNAL_SMTP_HOST_RE.test(url)) {
    throw new Error(`E2E blocked unexpected external mail host: ${url}`);
  }
}

export function isBlockedExternalService(url: string): boolean {
  try {
    const host = new URL(url).hostname.toLowerCase();
    if (EXTERNAL_SMTP_HOST_RE.test(url) || /smtp\.e2e\.invalid/i.test(url)) return true;
    if (EXTERNAL_HANDOVER_HOST_RE.test(host) || host === 'wa.me') return true;
    return false;
  } catch {
    return EXTERNAL_SMTP_HOST_RE.test(url);
  }
}
