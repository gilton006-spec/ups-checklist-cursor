# UPS Sunrise Checklist — Playwright E2E

End-to-end tests for **`UpsChecklist.Web`** (ASP.NET Core). They do **not** target the root Node/Vinext app or the Fly.io production site.

## Prerequisites

- .NET SDK 10.x
- Node.js 22+
- From this folder: `npm ci` (installs Playwright + Chromium)

## Safety

- Servers start with `ASPNETCORE_ENVIRONMENT=Testing`.
- SMTP password is empty; Ethereal is not provisioned in Testing.
- `helpers/safety.ts` refuses `PLAYWRIGHT_BASE_URL` hosts other than `localhost` / `127.0.0.1`.
- Site password is **`E2E_SITE_PASSWORD`** (default `e2e-test-only`). Never put the real Sunrise password in Git, screenshots, or CI logs.

## Commands

```bash
cd dotnet/e2e
npm ci
npm run test:e2e          # headless
npm run test:e2e:headed
npm run test:e2e:ui
npm run test:e2e:report   # open last HTML report
```

From the repository root (delegates here):

```bash
npm run test:e2e
npm run test:e2e:headed
npm run test:e2e:ui
npm run test:e2e:report
```

## What runs

| Project | Purpose |
| --- | --- |
| `setup` | Login once; write `auth/user.json` |
| `chromium` | Desktop Jambreaker, scanners, WhatsApp/email mocks |
| `mobile-android` | Galaxy-class viewport (~412×915) |
| `access` | Shared-password gate (no stored session) |
| `maintenance` | `APP_MAINTENANCE_MODE=true` on port 5089 + flag-file toggle on 5090 |

Three local Kestrel instances are started automatically on `5088`, `5089`, and `5090`.

## Maintenance mode (application feature)

| Variable | Effect |
| --- | --- |
| `APP_MAINTENANCE_MODE=true` | Durable emergency switch (preferred in production / Fly secrets) |
| `APP_MAINTENANCE_FLAG_FILE` | 503 while that file exists (local/E2E toggle only; **not** durable on Fly’s ephemeral disk; not controllable via any public HTTP endpoint) |

Responses use `Cache-Control: no-store`. Browser drafts are not deleted (nothing is stored server-side).

## Scenarios that are PDF-only or partially automated

- **Schedule warnings** (Monday / other-days): not shown in the Razor UI; warning **copy** is covered by Core unit tests. E2E asserts a non-empty PDF is still produced for mismatched dates (custom PDF text is not reliably extractable as plain strings).
- **Jambreaker “browser download”**: there is no standalone Download button. WhatsApp handover calls `/api/download`; tests assert `Content-Type`, `Content-Disposition` filename, and non-empty `%PDF` bytes. Scanner lists use a real Playwright `download` event.
- **Real WhatsApp / SMTP**: never exercised. Email UI is driven with route mocks for `/api/handover-config` and `/api/email-handover`.

## Synthetic data

- Name: `E2E Test User`
- Scanner: `TEST-001`
- Remarks: `Automated test data only`
- Dates: Monday `2026-09-07`, Tuesday `2026-09-08`, Saturday `2026-09-12`
