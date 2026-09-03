using System.Text;
using UpsChecklist.Core.Scanners;

namespace UpsChecklist.Core.Pdf;

/// <summary>Landscape A4 scanner form matching the Excel columns, fills and instruction line.</summary>
public sealed class ScannerPdfCreator(string? fontPath = null)
{
    // Excel Office theme fills, lightened with the workbook tint.
    private static readonly PdfColor HeaderBg = Tint(15, 158, 213, 0.80);
    private static readonly PdfColor TwoPieceBg = Tint(78, 167, 46, 0.80);
    private static readonly PdfColor InstructionBg = Tint(160, 43, 147, 0.80);
    private static readonly PdfColor MondayBg = Tint(78, 167, 46, 0.80);
    private static readonly PdfColor WeekdayBg = Tint(15, 158, 213, 0.40);
    private static readonly PdfColor Grid = new(0.50, 0.50, 0.50);

    private static PdfColor Tint(int r, int g, int b, double tint) =>
        new((r + (255 - r) * tint) / 255.0, (g + (255 - g) * tint) / 255.0, (b + (255 - b) * tint) / 255.0);

    public byte[] Create(ScannerSubmission data, string returnInstruction = "")
    {
        var (version, sheet) = ScannerValidator.Validate(data);
        _ = returnInstruction;
        var font = new ChecklistFont(fontPath ?? Path.Combine(AppContext.BaseDirectory, "Assets", "checklist-font.ttf"));
        var doc = new PdfDocumentWriter(font);
        const double w = 841.89, h = 595.28, margin = 28, size = 9, leading = 13;
        double[] widths = [150, 42, 72, 88, 118, 92, w - margin * 2 - 562];
        var page = default(PdfPageCanvas)!;
        var y = 0.0;

        List<string> Wrap(string value, double width)
        {
            var result = new List<string>();
            foreach (var paragraph in value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (font.Measure(candidate, size) <= width) { line.Clear().Append(candidate); continue; }
                    if (line.Length > 0) { result.Add(line.ToString()); line.Clear(); }
                    foreach (var rune in word.EnumerateRunes())
                    {
                        if (line.Length > 0 && font.Measure(line.ToString() + rune, size) > width)
                        { result.Add(line.ToString()); line.Clear(); }
                        line.Append(rune.ToString());
                    }
                }
                result.Add(line.ToString());
            }
            return result;
        }

        void NewPage()
        {
            page = doc.AddPage(w, h);
            var titleWidth = font.Measure(sheet.Title, 16);
            page.Text(sheet.Title, w - margin - titleWidth, h - 36, 16, PdfPalette.Ink);
            var day = version.Label;
            var dayWidth = font.Measure(day, 11) + 16;
            var dayBg = version.Id == "monday" ? MondayBg : WeekdayBg;
            page.Rectangle(w - margin - dayWidth, h - 62, dayWidth, 18, dayBg, true);
            page.Text(day, w - margin - dayWidth + 8, h - 57, 11, PdfPalette.Ink);
            page.Text($"Date: {data.Date}", margin, h - 40, 10, PdfPalette.Ink);
            y = h - 78;
            var x = margin;
            for (var i = 0; i < widths.Length; i++)
            {
                page.Rectangle(x, y - 22, widths[i], 22, HeaderBg, true);
                page.Rectangle(x, y - 22, widths[i], 22, Grid, false);
                page.Text(sheet.Headers[i], x + 5, y - 15, size, PdfPalette.Ink);
                x += widths[i];
            }
            y -= 22;
        }

        NewPage();
        foreach (var row in sheet.Rows)
        {
            var entry = data.Entries.GetValueOrDefault(row.Id) ?? new ScannerEntry();
            var values = new[] { row.UserName, row.Gost, row.ScannerType,
                row.Position.Length > 0 ? row.Position : entry.Position,
                entry.HandoverTo, entry.ScannerNumber, entry.Comments };
            var lines = values.Select((value, i) => Wrap(value, widths[i] - 12)).ToArray();
            var printedNote = string.IsNullOrEmpty(row.Note) ? [] : Wrap(row.Note, widths[6] - 12);
            var height = Math.Max(28, Math.Max(lines.Take(6).Max(l => l.Count), lines[6].Count + printedNote.Count) * leading + 12);
            if (y - height < 48) NewPage();
            if (y - height < 48)
                throw new ScannerValidationException($"{row.UserName}: reduce the number of lines in Comments to fit the PDF.");
            var x = margin;
            for (var col = 0; col < widths.Length; col++)
            {
                if (col == 2 && row.ScannerType == "Two piece")
                    page.Rectangle(x, y - height, widths[col], height, TwoPieceBg, true);
                page.Rectangle(x, y - height, widths[col], height, Grid, false);
                if (col >= 4 || col == 3 && row.Position.Length == 0)
                {
                    var noteHeight = col == 6 ? printedNote.Count * leading : 0;
                    if (col == 6)
                    {
                        for (var l = 0; l < printedNote.Count; l++)
                            page.Text(printedNote[l], x + 6, y - 12 - l * leading, size, PdfPalette.Muted);
                    }
                    var suffix = col switch { 3 => "position", 4 => "handoverTo", 5 => "scannerNumber", _ => "comments" };
                    doc.WrappedTextField(page, $"{row.Id}_{suffix}", values[col], string.Join('\n', lines[col]),
                        x + 3, y - height + 3, widths[col] - 6, height - 6 - noteHeight, size);
                }
                else
                {
                    for (var l = 0; l < lines[col].Count; l++)
                        page.Text(lines[col][l], x + 6, y - 13 - l * leading, size, PdfPalette.Ink);
                }
                x += widths[col];
            }
            y -= height;
        }

        foreach (var instruction in sheet.Instructions)
        {
            var lines = Wrap(instruction.Text, w - margin * 2 - 16);
            var height = Math.Max(24, lines.Count * leading + 10);
            if (y - height < 48) NewPage();
            page.Rectangle(margin, y - height, w - margin * 2, height, InstructionBg, true);
            page.Rectangle(margin, y - height, w - margin * 2, height, Grid, false);
            for (var l = 0; l < lines.Count; l++)
                page.Text(lines[l], margin + 8, y - 16 - l * leading, 10, PdfPalette.Ink);
            y -= height;
        }

        for (var i = 0; i < doc.Pages.Count; i++)
        {
            doc.Pages[i].Text($"Source: {version.SourceFile} / {sheet.SourceSheet}", margin, 22, 8, PdfPalette.Muted);
            doc.Pages[i].Text($"Page {i + 1} of {doc.Pages.Count}", w - margin - 80, 22, 9, PdfPalette.Muted);
        }
        return doc.Save($"{sheet.Title} - {data.Date}", $"{version.Label} scanner handover form");
    }
}
