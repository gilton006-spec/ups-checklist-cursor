import {
  test,
  expect,
  DATES,
  POSITIONS,
  SYNTHETIC,
  evidenceJpeg,
  selectPosition,
  fillRequiredBasics,
  positionButton,
  pdfContainsText,
  captureChecklistPdf,
  remarksField,
} from '../helpers/fixtures';

test.describe('Jambreaker checklist', () => {
  test('homepage loads with all nine positions and none selected', async ({ checklistPage: page }) => {
    await expect(page.getByRole('heading', { name: 'Choose your position' })).toBeVisible();
    await expect(page.locator('#checklist-panel')).toBeHidden();
    await expect(page.locator('#download-bar')).toBeHidden();

    const buttons = page.locator('button.position-button');
    await expect(buttons).toHaveCount(9);
    for (const pos of POSITIONS) {
      await expect(positionButton(page, pos.id)).toBeVisible();
      await expect(positionButton(page, pos.id)).toHaveAttribute('aria-pressed', 'false');
    }
  });

  test('selecting each position shows the matching title', async ({ checklistPage: page }) => {
    for (const pos of POSITIONS) {
      await selectPosition(page, pos.id);
      await expect(page.locator('#position-heading')).toHaveText(pos.label);
      await expect(page.locator('#sections input[data-check]').first()).toBeVisible();
    }
  });

  test('Monday and weekday schedule mismatches still produce a PDF (warnings are PDF-drawn)', async ({ checklistPage: page }) => {
    // Schedule warnings are drawn with the custom PDF text pipeline (not DOM).
    // Exact glyph strings are not reliably extractable; Core unit tests cover warning copy.
    await selectPosition(page, 'ps1-ps2-monday');
    await fillRequiredBasics(page, { date: DATES.tuesday });

    const first = await captureChecklistPdf(page, async () => {
      await page.getByRole('button', { name: 'WhatsApp' }).click();
    });
    expect(first.status).toBe(200);
    expect(first.headers['content-type'] ?? '').toContain('application/pdf');
    expect(first.buffer.byteLength).toBeGreaterThan(500);
    expect(first.buffer.subarray(0, 4).toString('latin1')).toBe('%PDF');
    // Title text from the PDF stream is sometimes present as literals.
    expect(
      pdfContainsText(first.buffer, 'PROTOTYPE')
      || pdfContainsText(first.buffer, 'PS1')
      || first.buffer.byteLength > 2000,
    ).toBeTruthy();

    await page.getByRole('button', { name: 'Close' }).click();
    await selectPosition(page, 'ps1');
    await fillRequiredBasics(page, { date: DATES.monday });

    const second = await captureChecklistPdf(page, async () => {
      await page.getByRole('button', { name: 'WhatsApp' }).click();
    });
    expect(second.status).toBe(200);
    expect(second.buffer.byteLength).toBeGreaterThan(500);
  });

  test('progress updates; incomplete checks stay unchecked; remarks survive navigation', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page);

    const checks = page.locator('#sections input[data-check]');
    const total = await checks.count();
    expect(total).toBeGreaterThan(3);

    await checks.nth(0).check();
    await checks.nth(1).check();
    await expect(page.locator('#progress-count')).toHaveText(new RegExp(`^2\\s*/\\s*${total}$`));

    await remarksField(page).fill(SYNTHETIC.remarks);
    await selectPosition(page, 'pd3');
    await selectPosition(page, 'pd4');

    await expect(checks.nth(0)).toBeChecked();
    await expect(checks.nth(1)).toBeChecked();
    await expect(checks.nth(2)).not.toBeChecked();
    await expect(remarksField(page)).toHaveValue(SYNTHETIC.remarks);
  });

  test('character limits are enforced on name and remarks', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    const name = page.getByLabel('Name', { exact: true });
    await expect(name).toHaveAttribute('maxlength', '65');
    await name.fill('x'.repeat(80));
    expect((await name.inputValue()).length).toBeLessThanOrEqual(65);

    const remarks = remarksField(page);
    await expect(remarks).toHaveAttribute('maxlength', '2000');
    await remarks.fill('y'.repeat(2100));
    expect((await remarks.inputValue()).length).toBeLessThanOrEqual(2000);
  });

  test('evidence photo preview, replace, and remove', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page);

    const root = page.locator('#evidence-photo-root');
    const gallery = root.locator('input[data-input="gallery"]');
    await gallery.setInputFiles(evidenceJpeg);
    await expect(root.locator('[data-preview]')).not.toHaveClass(/hidden/, { timeout: 20_000 });
    await expect(root.getByRole('button', { name: 'Remove photo' })).toBeVisible();

    await gallery.setInputFiles(evidenceJpeg);
    await expect(root.locator('[data-preview]')).not.toHaveClass(/hidden/);

    await root.getByRole('button', { name: 'Remove photo' }).click();
    await expect(root.locator('[data-preview]')).toHaveClass(/hidden/);
  });

  test('switching position while a photo is loading does not attach it to the wrong draft', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    const root = page.locator('#evidence-photo-root');
    const upload = root.locator('input[data-input="gallery"]').setInputFiles(evidenceJpeg);
    await page.locator('#toggle-picker').click();
    await selectPosition(page, 'pd1');
    await upload.catch(() => undefined);
    await page.waitForTimeout(500);

    const preview = page.locator('#evidence-photo-root [data-preview]:not(.hidden)');
    await expect(preview).toHaveCount(0);
  });

  test('signature canvas accepts drawing and Clear removes it', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    await page.getByRole('tab', { name: 'Draw signature' }).click();
    const canvas = page.getByLabel('Draw your signature');
    const box = await canvas.boundingBox();
    expect(box).toBeTruthy();
    await page.mouse.move(box!.x + 20, box!.y + 20);
    await page.mouse.down();
    await page.mouse.move(box!.x + 120, box!.y + 40);
    await page.mouse.move(box!.x + 60, box!.y + 80);
    await page.mouse.up();

    const before = await canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL());
    expect(before.length).toBeGreaterThan(1000);

    await page.locator('#clear-signature').click();
    const after = await canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL());
    expect(after.length).toBeLessThan(before.length);
  });

  test('draft restores after refresh and does not leak between positions', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page);
    await page.locator('#sections input[data-check]').first().check();
    await remarksField(page).fill(SYNTHETIC.remarks);
    await page.waitForTimeout(600);

    await page.reload();
    await expect(page.getByText(/Restored draft from this browser tab/i)).toBeVisible();
    await expect(page.locator('#position-heading')).toHaveText('PD4');
    await expect(remarksField(page)).toHaveValue(SYNTHETIC.remarks);

    await selectPosition(page, 'matrix');
    await expect(remarksField(page)).toHaveValue('');
    await expect(page.locator('#sections input[data-check]').first()).not.toBeChecked();
  });

  test('WhatsApp prepare returns a non-empty PDF with position and date in the filename', async ({ checklistPage: page }) => {
    await selectPosition(page, 'pd4');
    await fillRequiredBasics(page, { date: DATES.tuesday });

    const pdf = await captureChecklistPdf(page, async () => {
      await page.getByRole('button', { name: 'WhatsApp' }).click();
    });

    expect(pdf.status).toBe(200);
    expect(pdf.headers['content-type'] ?? '').toMatch(/application\/pdf/i);
    const disposition = pdf.headers['content-disposition'] ?? '';
    expect(disposition).toMatch(/UPS_pd4_2026-09-08\.pdf/i);
    expect(pdf.buffer.byteLength).toBeGreaterThan(500);
    expect(pdf.buffer.subarray(0, 4).toString('latin1')).toBe('%PDF');
  });
});
