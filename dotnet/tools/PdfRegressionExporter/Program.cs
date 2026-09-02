using System.Globalization;
using System.Text.Json;
using UpsChecklist.Core;
using UpsChecklist.Core.Models;
using UpsChecklist.Core.Pdf;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/PdfRegressionExporter -- <output-directory>");
    return 1;
}

// Only synthetic data. No HTTP requests, SMTP or WhatsApp activity.
var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);
var manifest = new List<object>();
foreach (var culture in new[] { "en-US", "nl-NL" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
    foreach (var position in ChecklistPositions.All)
    {
        Export(position, culture, "", "", "Safety inspection test. Follow up at handover.", "");
    }
}

foreach (var position in ChecklistPositions.All)
    Export(position, "nl-NL", "-jpeg", UpsChecklist.Tests.EvidenceFixtures.JpegPhoto,
        "Synthetic JPEG evidence test.", "");

var pd3 = ChecklistPositions.Get("pd3")!;
var imagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Workbook", "image13.png");
var photo = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(imagePath));
Export(pd3, "nl-NL", "-photo-signature", photo, "Photo and signature rendering test.", UpsChecklist.Tests.EvidenceFixtures.TransparentSignature);
Export(ChecklistPositions.Get("matrix")!, "nl-NL", "-long", "",
    string.Concat(Enumerable.Repeat("Long remark: recheck the area before handover. ", 43)), "");
Export(pd3, "nl-NL", "-unicode", "", "Café geprüft. Opmerking: naïef, façade, João.", "", "Zoë Müller");
Export(pd3, "nl-NL", "-blank", "", "", "", "", blank: true);

File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"),
    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {manifest.Count} synthetic PDFs and manifest.json to {outputDirectory}");
return 0;

void Export(Position position, string culture, string suffix, string photoData, string remarks, string drawn,
    string name = "QA Example", bool blank = false)
{
    var checks = ChecklistPositions.AllItems(position)
        .Select((item, index) => (item.Id, Value: !blank && index % 2 == 0))
        .ToDictionary(x => x.Id, x => x.Value);
    var data = new ChecklistSubmission
    {
        PositionId = position.Id,
        Name = name,
        Date = position.Schedule == "monday" ? "2026-09-07" : "2026-09-02",
        Count = blank ? "" : "7",
        Signature = name,
        Remarks = remarks,
        Checks = checks,
        SectionRemarks = position.Sections.Where(s => s.RemarksKey is not null)
            .ToDictionary(s => s.RemarksKey!, _ => "Section checked. No outstanding issue."),
        EvidencePhoto = photoData,
        Drawn = drawn,
    };
    var file = $"{position.Id}-{culture}{suffix}.pdf";
    File.WriteAllBytes(Path.Combine(outputDirectory, file), new ChecklistPdfCreator().Create(data));
    manifest.Add(new
    {
        file, positionId = position.Id, title = position.Title, culture,
        name = data.Name, date = data.Date, count = data.Count, signature = data.Signature,
        remarks, checks, drawn = drawn.Length != 0,
        minimumImages = position.Sections.Sum(s => s.Images?.Count ?? 0)
            + (photoData.Length != 0 ? 1 : 0) + (drawn.Length != 0 ? 1 : 0),
    });
}
