# PDF export fix

Base: `dotnet-migration` commit `190453a0051b2216e3d50ced852a0583dc6b3d51`.
The Node app, checklist definitions, source drawings, website UI, email routing, and WhatsApp behaviour are unchanged. This patch has not been pushed or deployed.

## What changed

The PDF generation path no longer uses Aspose.PDF.FOSS. A bounded, managed C# writer generates only the report features this app uses: pages, embedded TrueType text, PNG/JPEG images, editable text fields, and checkboxes. It is not an arbitrary PDF reader or general document-conversion library.

* All PDF numbers use invariant formatting, independent of the request culture.
* Page content is written at explicit coordinates, without mixing paragraph flow and low-level drawing.
* Reference drawings and photos are embedded with explicit image resources and isolated graphics states.
* PNG transparency is represented by a soft mask. PNGs are decoded with their filters, bit depths, palette and Adam7 interlacing; JPEG image streams are embedded directly.
* One canonical name/date field owns a separate widget on every page. Editing a shared field can update all its widgets.
* Every field has a complete normal appearance. Checked/unchecked appearances are vector paths, with no ZapfDingbats font dependency.
* Initial text appearances embed the complete bundled TrueType font. Field `/DA` uses the standard Helvetica resource as a portable editing fallback. A PDF viewer may therefore use its Helvetica equivalent when the user edits text.
* `NeedAppearances` is false. PDFs do not depend on a viewer repairing their appearances.
* Drawn signatures use the correct top coordinate and remain inside the signature box.

The bundled font supports the existing Latin checklist text and tested accented names. This is not a text-shaping engine for arbitrary writing systems; unsupported characters fail explicitly rather than silently disappearing.

## Run in Cursor

From the repository root, with the .NET 10 SDK:

```powershell
dotnet restore dotnet/UpsChecklist.slnx
dotnet build dotnet/UpsChecklist.slnx --no-restore
dotnet test dotnet/UpsChecklist.slnx --no-build
```

These full application/integration tests still need to be run on your computer. Package downloads were blocked in the review environment. The website, SMTP delivery, WhatsApp, and browser UI were not exercised here.

## Reproduce the offline PDF checks

The standalone exporter compiles the exact production PDF sources plus the checklist definitions, excluding the web/email dependencies. Its restore source is local and it needs no NuGet packages. It only writes synthetic reports and never calls SMTP, Ethereal, or WhatsApp.

```powershell
dotnet build dotnet/tools/PdfRegressionExporter --disable-build-servers -p:UseSharedCompilation=false
dotnet run --project dotnet/tools/PdfRegressionExporter --no-build -- outputs/pdf-regression
```

For two-renderer validation, install Python packages `pypdf`, `pymupdf`, and `pillow`, and make Poppler's `pdftoppm` available on PATH. Then:

```powershell
python dotnet/tools/validate_pdf_exports.py outputs/pdf-regression
```

The validator decompresses and inspects content/appearance streams, checks field values and widget relationships, renders every page using Poppler and MuPDF, checks that filled fields have visible ink in both renderers, and tests an edit/save/reopen roundtrip. Rendering outputs remain under the selected output directory for visual inspection. File size or an `/Image` marker in compressed bytes is not accepted as proof of an intact drawing.

## Verified here

* Standalone production PDF sources compile on .NET 10: zero warnings/errors.
* 31 synthetic exports pass both-renderer and structural checks: all nine positions in en-US and nl-NL, JPEG evidence for every position, PNG evidence/drawn signature, long remarks, accented text and a blank PD3 form.
* An additional PD3 edit/save/reopen passes, including the repeated name/date widgets.
* All nine en-US/nl-NL pairs are byte-for-byte identical.
* All 13 checklist reference images match the original decoded RGB pixels. The unused UPS logo is not part of the checklist drawings.
* Every normal position page was visually reviewed. No PDF syntax/font warnings were emitted by Poppler.

Acrobat and Chrome were not directly tested. Passing independent renderers is not a claim of UPS approval or operational sign-off.
