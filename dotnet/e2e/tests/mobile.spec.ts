import { test, expect, DATES, SYNTHETIC, selectPosition, fillRequiredBasics, evidenceJpeg, remarksField } from '../helpers/fixtures';

test.describe('Mobile usability (Galaxy-class viewport)', () => {
  test('no horizontal scroll; touch targets; position and signature usable', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Choose your position' })).toBeVisible();

    const scroll = await page.evaluate(() => ({
      doc: document.documentElement.scrollWidth,
      view: window.innerWidth,
    }));
    expect(scroll.doc).toBeLessThanOrEqual(scroll.view + 1);

    const first = page.locator('button.position-button').first();
    const box = await first.boundingBox();
    expect(box).toBeTruthy();
    expect(Math.min(box!.width, box!.height)).toBeGreaterThanOrEqual(40);

    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page, { date: DATES.tuesday });

    const whatsapp = page.getByRole('button', { name: 'WhatsApp' });
    await expect(whatsapp).toBeVisible();
    const wbox = await whatsapp.boundingBox();
    expect(wbox!.height).toBeGreaterThanOrEqual(40);

    const remarks = remarksField(page);
    await remarks.scrollIntoViewIfNeeded();
    await remarks.fill(SYNTHETIC.remarks);
    await expect(remarks).toBeEditable();

    await page.getByRole('tab', { name: 'Draw signature' }).click();
    const canvas = page.getByLabel('Draw your signature');
    const cbox = await canvas.boundingBox();
    await page.mouse.move(cbox!.x + 20, cbox!.y + 20);
    await page.mouse.down();
    await page.mouse.move(cbox!.x + 100, cbox!.y + 50);
    await page.mouse.up();

    await page.locator('#evidence-photo-root input[data-input="gallery"]').setInputFiles(evidenceJpeg);
    await expect(page.locator('#evidence-photo-root [data-preview]')).not.toHaveClass(/hidden/, { timeout: 20_000 });
  });

  test('keyboard can reach important actions', async ({ page }) => {
    await page.goto('/');
    await page.keyboard.press('Tab');
    const tag = await page.evaluate(() => document.activeElement?.tagName);
    expect(['A', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA']).toContain(tag);
  });
});
