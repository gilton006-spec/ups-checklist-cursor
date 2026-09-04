import {
  test,
  expect,
  DATES,
  SYNTHETIC,
  selectPosition,
  fillRequiredBasics,
  mockEmailConfigured,
  mockEmailHandoverSuccess,
  remarksField,
} from '../helpers/fixtures';

test.describe('WhatsApp and email safety', () => {
  test('WhatsApp opens a confirm dialog and does not put checklist data in wa.me URLs', async ({ checklistPage: page }) => {
    const external: string[] = [];
    page.on('request', (req) => {
      const url = req.url();
      if (/wa\.me|whatsapp\.com|api\.whatsapp/i.test(url)) external.push(url);
    });

    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page, { date: DATES.tuesday });
    await remarksField(page).fill(SYNTHETIC.remarks);

    await page.getByRole('button', { name: 'WhatsApp' }).click();
    await expect(page.getByRole('heading', { name: 'WhatsApp handover' })).toBeVisible();
    await expect(page).toHaveURL(/\/$/);
    expect(external).toEqual([]);

    const scripts = await page.locator('script').allTextContents();
    expect(scripts.join('\n')).not.toMatch(/wa\.me\/\d+\?text=.*Automated test data/i);
  });

  test('email uses intercepted mock transport and never hits real SMTP', async ({ checklistPage: page }) => {
    await mockEmailConfigured(page);
    await mockEmailHandoverSuccess(page);
    await page.reload();
    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page);

    await expect(page.getByRole('button', { name: 'Send report' })).toBeEnabled();
    await page.getByRole('button', { name: 'Send report' }).click();
    await expect(page.getByRole('heading', { name: /Send report to company/i })).toBeVisible();
    await expect(page.locator('#email-message')).toContainText(/Accepted by the mail transport/i);
    await expect(page.locator('#email-message')).toContainText(/not confirmed/i);
  });

  test('blocks unexpected external mail API hosts', async ({ page }) => {
    const outcome = await page.evaluate(async () => {
      try {
        await fetch('https://api.nodemailer.com/user', { method: 'POST', body: '{}' });
        return 'reached';
      } catch {
        return 'blocked';
      }
    });
    expect(outcome).toBe('blocked');
  });
});
