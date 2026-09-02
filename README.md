# UPS Sunrise Checklist

For Cursor setup, local commands, and Git handoff, start with [CURSOR_SETUP.md](CURSOR_SETUP.md).

## ASP.NET Core migration (.NET 10)

The **`dotnet-migration`** branch adds a C# / ASP.NET Core Razor Pages replacement under [`dotnet/`](dotnet/). The original Node.js application at the repository root is **unchanged** and remains available for comparison until you verify the new app.

| | Original (Node / Vinext) | New (ASP.NET Core) |
| --- | --- | --- |
| Runtime | Node.js 22+ | .NET 10 SDK only |
| Location | Repository root | [`dotnet/UpsChecklist.Web`](dotnet/UpsChecklist.Web) |
| Tests | `npm run test:local` (30 checks) | `dotnet test` plus the [PDF rendering checks](PDF_FIX_NOTES.md) |
| PDF output | pdf-lib (ISC) | Bounded C# PDF exporter, using the bundled font and .NET only |

### Prerequisites

Install the **.NET 10 SDK** (10.0.400 or newer):

```powershell
winget install Microsoft.DotNet.SDK.10 --accept-package-agreements --accept-source-agreements
dotnet --version
```

Node.js is **not** required to run the new application. It is still needed only if you work on the original app or run `node scripts/extract-assets.mjs` to refresh workbook images from source.

### Cursor: open and run the new app

1. **File → Open Folder** → select `UPS_Checklist_Cursor`.
2. Check out the migration branch: `git checkout dotnet-migration`
3. Open Cursor's terminal:

```powershell
cd dotnet
dotnet restore UpsChecklist.slnx
dotnet build UpsChecklist.slnx
dotnet test UpsChecklist.slnx
dotnet run --project UpsChecklist.Web
```

4. Open the URL printed by Kestrel (typically `https://localhost:7xxx` or `http://localhost:5xxx`).

First-time workbook images: if `dotnet/UpsChecklist.Web/wwwroot/workbook/` is empty, run once from the repo root:

```powershell
node scripts/extract-assets.mjs
```

### Solution layout

| Path | Purpose |
| --- | --- |
| [`dotnet/UpsChecklist.Web`](dotnet/UpsChecklist.Web) | Razor Pages UI, static JS, `/api/download` endpoint |
| [`dotnet/UpsChecklist.Core`](dotnet/UpsChecklist.Core) | Positions, validation, PDF generation |
| [`dotnet/UpsChecklist.Tests`](dotnet/UpsChecklist.Tests) | Automated migration tests |
| `app/`, `lib/`, `components/` | Original Node application (preserved) |

### Privacy and WhatsApp

Behaviour matches the original: no login, no permanent storage, resized photos, explicit WhatsApp confirmation to **+31 626149058**, and no automatic delivery claims. See [CURSOR_SETUP.md](CURSOR_SETUP.md) for details.

---

## Original starter notes

A clean full-stack starter running on [vinext](https://github.com/cloudflare/vinext), with optional Cloudflare D1 and Drizzle support.

## Prerequisites (original Node app)

- Node.js `>=22.13.0`
- Linux with `flock`, `curl`, and GNU `timeout`

## Sites Lifecycle

The Sites lifecycle CLI runs the locked dependency install before returning this checkout. Edit the source under `app/`, then checkpoint when a coherent milestone is ready to inspect or share. The remote Sites builder runs `npm run build` against the pushed commit. Do not repeat install or build as a normal pre-checkpoint step.

This starter does not use `wrangler.jsonc`.

`install:ci` is intentionally a single, non-retrying `npm ci`. It refuses a concurrent install for the same project, consumes a matching image-seeded npm cache with `--prefer-offline` while retaining registry fallback for a missing cache object, otherwise downloads and verifies the complete vinext tarball recorded in `package-lock.json`, limits npm to one socket, and terminates a stalled install. `build` applies a short timeout. These helpers target Linux and use GNU `timeout`; they are not native macOS scripts.

Scripts that need writable project-scoped home, npm, XDG, and temporary paths use `scripts/sites-env.sh`. The `dev` and `start` scripts honor the caller's runtime environment and keep Wrangler logs inside the checkout. The generated `.sites-runtime/` directory is disposable and ignored by Git.

## Included Shape

- edit site code under `app/`
- `app/chatgpt-auth.ts` provides optional dispatch-owned ChatGPT sign-in helpers
- `.openai/hosting.json` declares optional Sites D1 and R2 bindings
- `vite.config.ts` simulates declared bindings for local development
- `db/index.ts` reads the D1 binding from the Cloudflare Worker environment
- `db/schema.ts` starts intentionally empty
- `examples/d1/` contains an optional D1 example surface
- `drizzle.config.ts` supports local migration generation when needed

## Diagnostic Commands (original Node app)

- `npm run install:ci`: perform the one bounded lockfile install
- `npm run dev`: start the Vite/Vinext development server
- `npm run build`: build the deployable Sites artifact
- `npm run start`: start the built Vinext application
- `npm test`: build and verify the rendered development-preview metadata
- `npm run db:generate`: generate Drizzle migrations after schema changes

Use build commands for targeted diagnosis after a remote failure, not as part of the normal checkpoint path.

The timeout defaults can be overridden for a controlled canary with `SITES_INSTALL_TIMEOUT`, `SITES_INSTALL_KILL_AFTER`, `SITES_BUILD_TIMEOUT`, and `SITES_BUILD_KILL_AFTER`. A timeout fails the command; the helpers never retry an unchanged install or build.

## Learn More

- [vinext Documentation](https://github.com/cloudflare/vinext)
- [Drizzle D1 Guide](https://orm.drizzle.team/docs/get-started/d1-new)
