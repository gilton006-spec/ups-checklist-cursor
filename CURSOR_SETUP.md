# UPS Sunrise Checklist: continue in Cursor

This export includes the complete source and Git commit history. It contains the latest photo evidence and WhatsApp handover implementation for all nine positions. These latest changes are not yet deployed to the public site.

## Start locally

1. Extract the ZIP into a folder you control.
2. In Cursor, choose **File > Open Folder** and select `UPS_Checklist_Cursor`.
3. Install Node.js 22.13 or newer (Node 22 LTS recommended), including npm. Install Git to use the commit history.
4. Open Cursor's terminal and run:

```sh
npm ci
npm run dev:local
```

Open the local address printed by Vite in your browser. No API key, ChatGPT login, or WhatsApp Business account is needed to develop the form or generate its PDFs.

The `:local` scripts avoid Bash and GNU timeout, so they can be launched from Windows PowerShell, macOS, or Linux. They have been tested in the provided Linux workspace; a real Windows/macOS install and phone camera/share sheet still need testing on your device. If the Cloudflare local runtime fails on native Windows, use Cursor with WSL and Node installed inside WSL.

## Build and test

```sh
npm run test:local
npm run check:local
```

The test command builds first and runs the 30 automated checks. It exercises all nine PDF variants, PNG/JPEG evidence embedding, upload limits, the configured WhatsApp number, file-share fallback and cancellation. The tests never send WhatsApp messages. Generated fixtures go into ignored `outputs/qa`.

The original `npm run build` and `npm run install:ci` scripts are preserved for the hosted Linux workflow. Prefer the `:local` scripts in Cursor.

## Main files

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

The configured handover number is **+31 626149058**. Change it and the normalized chat URL together in `lib/whatsapp-handover.ts`; update its exact-number tests if changed.

Clicking **WhatsApp** prepares the current report. On compatible devices, **Share PDF** opens the system share sheet: the worker chooses WhatsApp, checks the recipient, and confirms sending. A browser cannot preselect both WhatsApp and the recipient for a file share.

The fallback is **Save PDF**, then **Open WhatsApp chat**, and manually attach the saved PDF. The chat link targets the configured number but cannot automatically attach the PDF. The evidence photo is inside the PDF. There is no automatic sending, Business API integration, delivery receipt or delivery tracking.

Photos are resized and re-encoded locally before PDF generation. One optional photo is held separately for each position. Drafts and photos disappear on refresh; no database, cloud file archive or localStorage record is used. Generating a PDF sends form data and the resized photo to the site's own hosted endpoint. It does not automatically send them to UPS or WhatsApp.

## Git and publishing

The extracted folder is already a Git repository. The export has no Git remote or publishing credentials, so it cannot accidentally push to the hosted site. Use `git log --oneline` or Cursor's Source Control panel to inspect history.

To create your own remote, create an empty **private** repository in your Git provider, then use its supplied URL:

```sh
git remote add origin YOUR_PRIVATE_REPOSITORY_URL
git push -u origin main
```

No GitHub repository was created as part of this export. A local commit or push to your new remote does not update the existing Sites website.

Keep `.openai/hosting.json` and the Sites build plugin if you intend to continue publishing the existing Site through ChatGPT Sites. Its project identifier is configuration, not a credential. Publishing still requires authorized access and explicit approval for this public site. Keep this repository private unless you have permission to publish the internal diagrams and configured phone number. This remains an unapproved prototype, not an official UPS system.

## Export contents

Included: app source, assets, extracted workbook data, package lockfile, tests, platform configuration and all project commits.

Excluded: installed dependencies, build output, runtime caches, test output, local environment files and credentials. The original uploaded Excel file is not required to run the app; its extracted data and diagrams are included. Keep your original workbook separately if you want to re-extract it later.
