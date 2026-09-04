import { test, expect, DATES, SYNTHETIC, scannerList } from '../helpers/fixtures';
import { assertLocalBaseUrl } from '../helpers/safety';

test.describe('Scanner lists', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    assertLocalBaseUrl(baseURL);
    await page.goto('/Scanners');
    await expect(page.getByRole('heading', { name: 'Scanner lists' })).toBeVisible();
  });

  test('Monday and weekday versions; weekend shows warning', async ({ page }) => {
    await page.locator('#scanner-date').fill(DATES.monday);
    await expect(page.locator('#scanner-version')).toHaveText(/Monday/i);
    await expect(scannerList(page)).toBeEnabled();

    await page.locator('#scanner-date').fill(DATES.tuesday);
    await expect(page.locator('#scanner-version')).toHaveText(/Tuesday/i);

    await page.locator('#scanner-date').fill(DATES.saturday);
    await expect(page.locator('#scanner-unavailable')).toContainText(/No scanner list is supplied for this date/i);
    await expect(scannerList(page)).toBeDisabled();
  });

  test('every scanner list remains selectable and entries stay isolated', async ({ page }) => {
    await page.locator('#scanner-date').fill(DATES.monday);
    const list = scannerList(page);
    const options = list.locator('option');
    const count = await options.count();
    expect(count).toBeGreaterThanOrEqual(4);

    const values: string[] = [];
    for (let i = 0; i < count; i++) {
      const value = await options.nth(i).getAttribute('value');
      if (value) values.push(value);
    }

    for (const value of values) {
      await list.selectOption(value);
      await expect(page.locator('#scanner-list-panel')).toBeVisible();
      await expect(page.locator('#scanner-rows table.scanner-sheet')).toBeVisible();
    }

    await list.selectOption(values[0]);
    await page.locator('#scanner-rows input').first().fill(SYNTHETIC.handoverTo);

    await page.locator('#scanner-date').fill(DATES.tuesday);
    await list.selectOption({ index: 0 });
    await expect(page.locator('#scanner-rows input').first()).toHaveValue('');

    await page.locator('#scanner-date').fill(DATES.monday);
    await list.selectOption(values[0]);
    await expect(page.locator('#scanner-rows input').first()).toHaveValue(SYNTHETIC.handoverTo);
  });

  test('progress only counts meaningful assignments', async ({ page }) => {
    await page.locator('#scanner-date').fill(DATES.tuesday);
    await scannerList(page).selectOption({ index: 0 });

    await expect(page.locator('#scanner-progress')).toContainText(/0 \/ \d+ assigned/);

    const handover = page.getByLabel(/Handover to/).first();
    await handover.fill('   ');
    await expect(page.locator('#scanner-progress')).toContainText(/0 \/ \d+ assigned/);

    await handover.fill(SYNTHETIC.handoverTo);
    await page.getByLabel(/Scanner#/).first().fill(SYNTHETIC.scanner);
    await expect(page.locator('#scanner-progress')).toContainText(/1 \/ \d+ assigned/);
  });

  test('scanner PDF download is non-empty with version, sheet, and date in the filename', async ({ page }) => {
    await page.locator('#scanner-date').fill(DATES.monday);
    await scannerList(page).selectOption('pd1');

    const downloadPromise = page.waitForEvent('download');
    const responsePromise = page.waitForResponse((r) =>
      r.url().includes('/api/scanners/download') && r.request().method() === 'POST');

    await page.getByRole('button', { name: 'Download PDF' }).click();
    const [download, response] = await Promise.all([downloadPromise, responsePromise]);

    expect(response.ok()).toBeTruthy();
    expect(response.headers()['content-type'] ?? '').toMatch(/application\/pdf/i);

    const headers = response.request().headers();
    expect(headers['requestverificationtoken']).toBeTruthy();

    expect(download.suggestedFilename()).toMatch(/^UPS_scanners_monday_pd1_2026-09-07\.pdf$/i);

    const filePath = await download.path();
    expect(filePath).toBeTruthy();
    const fs = await import('node:fs');
    const bytes = fs.readFileSync(filePath!);
    expect(bytes.byteLength).toBeGreaterThan(500);
    expect(bytes.subarray(0, 4).toString('latin1')).toBe('%PDF');
  });

  test('missing antiforgery token is rejected safely', async ({ page, baseURL }) => {
    assertLocalBaseUrl(baseURL);
    const res = await page.request.post(`${baseURL}/api/scanners/download`, {
      data: {
        date: DATES.monday,
        versionId: 'monday',
        sheetId: 'pd1',
        entries: {},
      },
      headers: { 'Content-Type': 'application/json' },
    });
    expect([400, 401, 403]).toContain(res.status());
  });
});
