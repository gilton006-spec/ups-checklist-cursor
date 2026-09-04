import { test as base, expect, type Page, type Locator } from '@playwright/test';
import path from 'node:path';
import { assertLocalBaseUrl, isBlockedExternalService } from './safety';

export const DATES = {
  monday: '2026-09-07',
  tuesday: '2026-09-08',
  saturday: '2026-09-12',
} as const;

export const SYNTHETIC = {
  name: 'E2E Test User',
  scanner: 'TEST-001',
  remarks: 'Automated test data only',
  handoverTo: 'E2E Leader',
} as const;

export const POSITIONS = [
  { id: 'ps1-ps2-monday', label: 'PS1 / PS2' },
  { id: 'ps1', label: 'PS1' },
  { id: 'ps2', label: 'PS2' },
  { id: 'pd1', label: 'PD1' },
  { id: 'pd2', label: 'PD2' },
  { id: 'pd3', label: 'PD3' },
  { id: 'pd4', label: 'PD4' },
  { id: 'smalls', label: 'Smalls' },
  { id: 'matrix', label: 'Matrix' },
] as const;

export const evidenceJpeg = path.join(__dirname, '..', 'fixtures', 'evidence-tiny.jpg');

type Fixtures = {
  checklistPage: Page;
  blockExternalMail: void;
};

export const test = base.extend<Fixtures>({
  blockExternalMail: [
    async ({ page }, use) => {
      await page.route('**/*', async (route) => {
        const url = route.request().url();
        if (isBlockedExternalService(url)) {
          await route.abort('blockedbyclient');
          return;
        }
        await route.continue();
      });
      await use();
    },
    { auto: true },
  ],

  checklistPage: async ({ page, baseURL }, use) => {
    assertLocalBaseUrl(baseURL);
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Choose your position' })).toBeVisible();
    await use(page);
  },
});

export { expect };

export function positionButton(page: Page, id: string): Locator {
  return page.locator(`button.position-button[data-id="${id}"]`);
}

export async function selectPosition(page: Page, id: string): Promise<void> {
  const picker = page.locator('#position-picker');
  const hidden = await picker.evaluate((el) => el.classList.contains('hidden')).catch(() => false);
  if (hidden) {
    await page.locator('#toggle-picker').click();
  }
  const btn = positionButton(page, id);
  await expect(btn).toBeVisible();
  await btn.click();
  await expect(page.locator('#position-heading')).toBeVisible();
  await expect(page.locator('#checklist-panel')).toBeVisible();
}

export async function fillRequiredBasics(page: Page, opts?: { date?: string; name?: string }): Promise<void> {
  await page.getByLabel('Date', { exact: true }).fill(opts?.date ?? DATES.tuesday);
  await page.getByLabel('Name', { exact: true }).fill(opts?.name ?? SYNTHETIC.name);
}

export async function mockEmailConfigured(page: Page): Promise<void> {
  await page.route('**/api/handover-config', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        emailAddress: 'e2e-mock@invalid.local',
        emailConfigured: true,
        whatsappNumber: '+31 626149058',
        usesTempInbox: false,
        devInboxUrl: null,
      }),
    });
  });
}

export async function mockEmailHandoverSuccess(page: Page): Promise<void> {
  await page.route('**/api/email-handover', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        address: 'e2e-mock@invalid.local',
        message: 'Accepted by the mail transport for e2e-mock@invalid.local. Delivery to the inbox is not confirmed.',
        deliveryConfirmed: false,
        usesTempInbox: false,
        devInboxUrl: null,
      }),
    });
  });
}

export async function captureChecklistPdf(page: Page, trigger: () => Promise<void>): Promise<{ buffer: Buffer; headers: Record<string, string>; status: number }> {
  let captured: { buffer: Buffer; headers: Record<string, string>; status: number } | null = null;
  await page.route('**/api/download', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }
    const response = await route.fetch();
    const body = Buffer.from(await response.body());
    const headers: Record<string, string> = {};
    for (const [key, value] of Object.entries(response.headers())) headers[key] = value;
    captured = { buffer: body, headers, status: response.status() };
    await route.fulfill({ status: response.status(), headers: response.headers(), body });
  });
  await trigger();
  await expect.poll(() => captured).not.toBeNull();
  await page.unroute('**/api/download');
  return captured!;
}

export function remarksField(page: Page) {
  return page.locator('#remarks');
}

export function scannerList(page: Page) {
  return page.locator('#scanner-sheet');
}

/** PDF bytes often embed plain Latin text; good enough for schedule-warning assertions. */
export function pdfContainsText(buffer: Buffer, needle: string): boolean {
  return buffer.toString('latin1').includes(needle);
}
