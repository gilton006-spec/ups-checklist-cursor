import { test, expect } from '@playwright/test';
import { assertLocalBaseUrl } from '../helpers/safety';

/**
 * Shared-password gate is present (SiteAccess cookie).
 * Password comes from E2E_SITE_PASSWORD (default e2e-test-only) — never the real Sunrise secret.
 */
test.describe('Site access protection', () => {
  test('incorrect password is rejected', async ({ page, baseURL }) => {
    assertLocalBaseUrl(baseURL);
    await page.goto('/Login');
    await page.getByLabel('Password').fill('definitely-wrong-password');
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByRole('alert')).toContainText(/Wrong password/i);
    await expect(page).toHaveURL(/Login/i);
  });

  test('successful access sets a secure HttpOnly site cookie', async ({ page, context, baseURL }) => {
    assertLocalBaseUrl(baseURL);
    const password = process.env.E2E_SITE_PASSWORD || 'e2e-test-only';
    await page.goto('/Login');
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByRole('heading', { name: 'Choose your position' })).toBeVisible();

    const cookies = await context.cookies();
    const access = cookies.find((c) => c.name === 'ups_site_access');
    expect(access).toBeTruthy();
    expect(access!.httpOnly).toBeTruthy();
    // SameAsRequest: on http://127.0.0.1 secure may be false; still HttpOnly + Lax.
    expect(access!.sameSite === 'Lax' || access!.sameSite === 'Strict').toBeTruthy();
  });

  test('unauthenticated visitors are redirected to login', async ({ browser, baseURL }) => {
    assertLocalBaseUrl(baseURL);
    const context = await browser.newContext();
    const page = await context.newPage();
    const res = await page.goto('/');
    expect(res?.status()).toBeLessThan(400);
    await expect(page).toHaveURL(/Login/i);
    await context.close();
  });

  test('password value is not written into the login page HTML', async ({ page, baseURL }) => {
    assertLocalBaseUrl(baseURL);
    const password = process.env.E2E_SITE_PASSWORD || 'e2e-test-only';
    await page.goto('/Login');
    const html = await page.content();
    expect(html).not.toContain(`value="${password}"`);
  });
});
