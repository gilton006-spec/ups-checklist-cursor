using System.Text.Json;
using UglyToad.PdfPig;
using UpsChecklist.Core;
using UpsChecklist.Core.Models;
using UpsChecklist.Core.Pdf;

var target = args.Length > 0 ? args[0] : null;
if (target is not null && File.Exists(target))
{
    using var pdf = PdfDocument.Open(target);
    Console.WriteLine($"File: {target}");
    Console.WriteLine($"Pages: {pdf.NumberOfPages}, Size: {new FileInfo(target).Length} bytes");
    for (var i = 1; i <= pdf.NumberOfPages; i++)
    {
        var page = pdf.GetPage(i);
        Console.WriteLine($"--- Page {i} ---");
        Console.WriteLine($"Text length: {page.Text.Length}");
        Console.WriteLine(page.Text[..Math.Min(400, page.Text.Length)].Replace('\n', '|'));
        Console.WriteLine($"Images: {page.GetImages().Count()}");
    }

    if (pdf.TryGetForm(out var form))
        foreach (var field in form.Fields)
            Console.WriteLine($"Field {field.Information.PartialName}: {field.FieldType}");
    return;
}

var creator = new ChecklistPdfCreator();
var position = ChecklistPositions.Get("pd3")!;
var data = new ChecklistSubmission
{
    PositionId = position.Id,
    Date = "2026-09-02",
    Name = "twtst",
    Checks = ChecklistPositions.AllItems(position).ToDictionary(i => i.Id, _ => true),
    Count = "3",
    Remarks = "test remark",
    Signature = "twtst",
};
var bytes = creator.Create(data);
var outPath = Path.Combine(AppContext.BaseDirectory, "probe-out.pdf");
File.WriteAllBytes(outPath, bytes);
Console.WriteLine($"Wrote {outPath} ({bytes.Length} bytes)");
using (var pdf = PdfDocument.Open(bytes))
{
    Console.WriteLine($"Pages: {pdf.NumberOfPages}");
    Console.WriteLine($"Page 1 text: {pdf.GetPage(1).Text[..Math.Min(300, pdf.GetPage(1).Text.Length)]}...");
    Console.WriteLine($"Page 1 images: {pdf.GetPage(1).GetImages().Count()}");
}
