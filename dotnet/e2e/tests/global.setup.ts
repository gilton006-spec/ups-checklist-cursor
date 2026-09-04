import { test as setup, expect } from '@playwright/test';
import path from 'node:path';
import { assertLocalBaseUrl } from '../helpers/safety';

const authFile = path.join(__dirname, '..', 'auth', 'user.json');

setup('authenticate with test-only site password', async ({ page, baseURL }) => {
  assertLocalBaseUrl(baseURL);
  const password = process.env.E2E_SITE_PASSWORD || 'e2e-test-only';

  await page.goto('/Login');
  await expect(page.getByRole('heading', { name: 'Enter shift password' })).toBeVisible();
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Continue' }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole('heading', { name: 'Choose your position' })).toBeVisible();

  await page.context().storageState({ path: authFile });
});
