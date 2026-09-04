import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';
import { assertLocalBaseUrl } from './helpers/safety';

const baseURL = process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:5088';
const maintenanceURL = process.env.PLAYWRIGHT_MAINTENANCE_URL || 'http://127.0.0.1:5089';
const flagURL = process.env.PLAYWRIGHT_FLAG_URL || 'http://127.0.0.1:5090';
assertLocalBaseUrl(baseURL, { allowUnset: true });
assertLocalBaseUrl(maintenanceURL);
assertLocalBaseUrl(flagURL);

const reuse = !process.env.CI;

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],
  timeout: 90_000,
  expect: { timeout: 15_000 },
  outputDir: 'test-results',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 20_000,
  },
  webServer: {
    command: 'node start-servers.mjs',
    url: `${baseURL}/health`,
    reuseExistingServer: reuse,
    timeout: 180_000,
  },
  projects: [
    {
      name: 'setup',
      testMatch: /global\.setup\.ts/,
    },
    {
      name: 'chromium',
      dependencies: ['setup'],
      use: {
        ...devices['Desktop Chrome'],
        storageState: path.join(__dirname, 'auth/user.json'),
      },
      testIgnore: [/maintenance\.spec\.ts/, /access\.spec\.ts/, /global\.setup\.ts/, /mobile\.spec\.ts/],
    },
    {
      name: 'mobile-android',
      dependencies: ['setup'],
      use: {
        ...devices['Pixel 7'],
        viewport: { width: 412, height: 915 },
        isMobile: true,
        hasTouch: true,
        storageState: path.join(__dirname, 'auth/user.json'),
      },
      testMatch: /mobile\.spec\.ts/,
    },
    {
      name: 'access',
      use: {
        ...devices['Desktop Chrome'],
        storageState: { cookies: [], origins: [] },
      },
      testMatch: /access\.spec\.ts/,
    },
    {
      name: 'maintenance',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: maintenanceURL,
        storageState: { cookies: [], origins: [] },
      },
      testMatch: /maintenance\.spec\.ts/,
    },
  ],
});
