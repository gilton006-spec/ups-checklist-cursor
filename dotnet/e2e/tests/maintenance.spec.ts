import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import { assertLocalBaseUrl } from '../helpers/safety';
import { DATES } from '../helpers/fixtures';

const maintenanceURL = process.env.PLAYWRIGHT_MAINTENANCE_URL || 'http://127.0.0.1:5089';
const flagURL = process.env.PLAYWRIGHT_FLAG_URL || 'http://127.0.0.1:5090';
const flagFile = process.env.APP_MAINTENANCE_FLAG_FILE
  || path.join(__dirname, '..', '.maintenance-flag');

test.describe('Emergency maintenance', () => {
  test('homepage, scanners, APIs, and static images return 503 with no-store; health stays 200', async ({ request }) => {
    assertLocalBaseUrl(maintenanceURL);

    const health = await request.get(`${maintenanceURL}/health`);
    expect(health.status()).toBe(200);
    expect(await health.json()).toEqual({ status: 'ok' });

    for (const pathName of ['/', '/Scanners', '/workbook/image1.png', '/css/checklist.css']) {
      const res = await request.get(`${maintenanceURL}${pathName}`);
      expect(res.status(), pathName).toBe(503);
      const cache = res.headers()['cache-control'] ?? '';
      expect(cache, pathName).toMatch(/no-store/i);
    }

    const pdf = await request.post(`${maintenanceURL}/api/download`, {
      form: { checklist: '{}' },
    });
    expect(pdf.status()).toBe(503);

    const email = await request.post(`${maintenanceURL}/api/email-handover`, {
      form: { checklist: '{}' },
    });
    expect(email.status()).toBe(503);
  });

  test('maintenance page is readable on a mobile viewport', async ({ browser }) => {
    const context = await browser.newContext({
      baseURL: maintenanceURL,
      viewport: { width: 412, height: 915 },
      isMobile: true,
      hasTouch: true,
    });
    const page = await context.newPage();
    const res = await page.goto('/');
    expect(res?.status()).toBe(503);
    await expect(page.getByRole('heading', { name: 'Temporarily offline' })).toBeVisible();
    await expect(page.getByText(/emergency maintenance/i)).toBeVisible();
    await context.close();
  });

  test('a tab opened before activation cannot submit afterward', async ({ browser }) => {
    assertLocalBaseUrl(flagURL);
    if (fs.existsSync(flagFile)) fs.unlinkSync(flagFile);

    const context = await browser.newContext({ baseURL: flagURL });
    const page = await context.newPage();
    const open = await page.goto('/');
    expect(open?.status()).toBe(200);
    await expect(page.getByRole('heading', { name: 'Choose your position' })).toBeVisible();

    fs.writeFileSync(flagFile, 'maintenance-on');
    try {
      const submit = await page.request.post(`${flagURL}/api/download`, {
        form: { checklist: JSON.stringify({ positionId: 'pd4', date: DATES.tuesday }) },
      });
      expect(submit.status()).toBe(503);

      const reload = await page.goto('/');
      expect(reload?.status()).toBe(503);
      await expect(page.getByRole('heading', { name: 'Temporarily offline' })).toBeVisible();
    } finally {
      if (fs.existsSync(flagFile)) fs.unlinkSync(flagFile);
    }
    await context.close();
  });

  test('maintenance does not claim to delete report information', async ({ request }) => {
    const res = await request.get(`${maintenanceURL}/`);
    const body = await res.text();
    expect(body.toLowerCase()).toMatch(/not deleted|nothing is archived/);
    expect(body.toLowerCase()).not.toMatch(/deleted your draft|wiped your report/);
  });
});
