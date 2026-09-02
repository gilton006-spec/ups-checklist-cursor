# UPS Sunrise Checklist: continue in Cursor

This export includes the complete source and Git commit history. It contains the latest photo evidence and WhatsApp handover implementation for all nine positions.

## ASP.NET Core app (.NET 10) — recommended on `dotnet-migration`

The C# migration lives under [`dotnet/`](dotnet/). It preserves the original Node app at the repository root until you verify the replacement.

### Prerequisites

1. Install the **.NET 10 SDK**:

```powershell
winget install Microsoft.DotNet.SDK.10 --accept-package-agreements --accept-source-agreements
dotnet --version
```

Expected output: `10.0.400` or newer.

2. Install Git to use the commit history and branch.

Node.js is **not** required for the new app. Install Node.js 22+ only if you also run the original application or refresh extracted workbook assets.

### Start the new app in Cursor

1. Extract or clone into a folder you control.
2. **File → Open Folder** → select `UPS_Checklist_Cursor`.
3. Switch to the migration branch:

```powershell
git checkout dotnet-migration
```

4. If workbook images are missing, extract them once from the repo root:

```powershell
node scripts/extract-assets.mjs
```

5. Build, test, and run from Cursor's terminal:

```powershell
cd dotnet
dotnet restore UpsChecklist.slnx
dotnet build UpsChecklist.slnx
dotnet test UpsChecklist.slnx
dotnet run --project UpsChecklist.Web
```

6. Open the local address printed by Kestrel in your browser.

No API key, login, or WhatsApp Business account is needed to develop the form or generate PDFs.

### Build and test (ASP.NET Core)

```powershell
cd dotnet
dotnet build UpsChecklist.slnx
dotnet test UpsChecklist.slnx
```

The test suite covers all nine PDF variants, PNG/JPEG evidence embedding, upload limits, the configured WhatsApp number, and validation rules. Tests never send WhatsApp messages.

### Main files (ASP.NET Core)

| File | Purpose |
| --- | --- |
| [`dotnet/UpsChecklist.Web/Pages/Index.cshtml`](dotnet/UpsChecklist.Web/Pages/Index.cshtml) | Checklist shell and WhatsApp dialog |
| [`dotnet/UpsChecklist.Web/wwwroot/js/checklist.js`](dotnet/UpsChecklist.Web/wwwroot/js/checklist.js) | Position selection, per-position drafts, UI |
| [`dotnet/UpsChecklist.Web/wwwroot/js/evidence-photo.js`](dotnet/UpsChecklist.Web/wwwroot/js/evidence-photo.js) | Camera/gallery, resize, preview, removal |
| [`dotnet/UpsChecklist.Web/wwwroot/js/whatsapp-handover.js`](dotnet/UpsChecklist.Web/wwwroot/js/whatsapp-handover.js) | Share, save, open-chat; never claims delivery |
| [`dotnet/UpsChecklist.Core/ChecklistPositions.cs`](dotnet/UpsChecklist.Core/ChecklistPositions.cs) | All nine position checklists |
| [`dotnet/UpsChecklist.Core/Pdf/ChecklistPdfCreator.cs`](dotnet/UpsChecklist.Core/Pdf/ChecklistPdfCreator.cs) | Editable PDFs, images, evidence, signatures |
| [`dotnet/UpsChecklist.Web/Program.cs`](dotnet/UpsChecklist.Web/Program.cs) | Validated `/api/download` endpoint |
| [`dotnet/UpsChecklist.Tests/`](dotnet/UpsChecklist.Tests/) | Automated migration tests |

Commercial dependencies: **Aspose.PDF.FOSS** (MIT) for editable PDF form fields; **PdfPig** (Apache 2.0) in tests only.

---

## Original Node.js app (preserved)

The Vinext/React application at the repository root remains fully functional.

### Prerequisites (original)

- Node.js 22.13 or newer (22 LTS recommended)
- Git

### Start locally (original)

```powershell
npm ci
npm run dev:local
```

Open the local address printed by Vite. The `:local` scripts work from Windows PowerShell without Bash or GNU `timeout`.

### Build and test (original)

```powershell
npm run test:local
npm run check:local
```

The test command builds first and runs 30 automated checks against the Node application.

### Main files (original)

| File | Purpose |
| --- | --- |
| `app/page.tsx` | Form, position selection and position-specific draft state |
| `app/globals.css` | Mobile layout and visual styling |
| `components/evidence-photo.tsx` | Camera/gallery, image resize, preview and removal |
| `components/whatsapp-handover.tsx` | Review, share, save and open-chat dialog |
| `lib/whatsapp-handover.ts` | Recipient number, chat URL, PDF preparation and native sharing |
| `lib/checklist-positions.ts` | All nine position checklists |
| `lib/workbook-source.json` | Extracted source workbook cell text and image associations |
| `public/workbook` | Original diagrams extracted from the workbook |
| `app/api/download/route.ts` | Validated PDF endpoint; no record storage |
| `lib/create-checklist-pdf.ts` | Editable PDFs, images, evidence and signatures |
| `tests` | Automated tests and non-personal fixtures |

## WhatsApp behavior and limitations

The configured handover number is **+31 626149058**. In the ASP.NET app, change it in [`dotnet/UpsChecklist.Core/WhatsAppConstants.cs`](dotnet/UpsChecklist.Core/WhatsAppConstants.cs) and update tests. In the Node app, change [`lib/whatsapp-handover.ts`](lib/whatsapp-handover.ts).

Clicking **WhatsApp** prepares the current report. On compatible devices, **Share PDF** opens the system share sheet. The fallback is **Save PDF**, then **Open WhatsApp chat**, and manually attach the saved PDF. Opening a chat does not attach or send the report. There is no automatic sending, delivery receipt, or delivery tracking.

Photos are resized and re-encoded locally before PDF generation. One optional photo is held separately for each position. Drafts and photos disappear on refresh; no database or localStorage is used.

## Git and publishing

The extracted folder is already a Git repository. Use `git log --oneline` or Cursor's Source Control panel to inspect history.

```powershell
git remote add origin YOUR_PRIVATE_REPOSITORY_URL
git push -u origin dotnet-migration
```

Keep this repository private unless you have permission to publish the internal diagrams and configured phone number. This remains an unapproved prototype, not an official UPS system.

## Export contents

Included: both application stacks, assets, extracted workbook data, package lockfile, tests, platform configuration, and project commits.

Excluded: installed dependencies, build output, runtime caches, test output, local environment files and credentials.
