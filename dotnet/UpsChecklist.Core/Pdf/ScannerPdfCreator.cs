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
        var returnLine = returnInstruction.Trim();
        var font = new ChecklistFont(fontPath ?? Path.Combine(AppContext.BaseDirectory, "Assets", "checklist-font.ttf"));
        var doc = new PdfDocumentWriter(font);
        const double w = ScannerPdfLayout.PageWidth, h = ScannerPdfLayout.PageHeight,
            margin = ScannerPdfLayout.Margin, size = ScannerPdfLayout.FontSize,
            noteSize = ScannerPdfLayout.NoteFontSize, bottom = ScannerPdfLayout.ContentBottom;
        // One line height for cell text and for the text inside editable fields.
        var leading = PdfFormAppearance.LineHeight(size);
        var noteLeading = PdfFormAppearance.LineHeight(noteSize);
        double[] widths = [150, 42, 72, 88, 118, 92, w - margin * 2 - 562];
        var page = default(PdfPageCanvas)!;
        var y = 0.0;

        List<string> Wrap(string value, double width, double textSize)
        {
            var result = new List<string>();
            foreach (var paragraph in value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (font.Measure(candidate, textSize) <= width) { line.Clear().Append(candidate); continue; }
                    if (line.Length > 0) { result.Add(line.ToString()); line.Clear(); }
                    foreach (var rune in word.EnumerateRunes())
                    {
                        if (line.Length > 0 && font.Measure(line.ToString() + rune, textSize) > width)
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
            page.Text($"Date: {data.Date}", margin, h - 40, noteSize, PdfPalette.Ink);
            y = ScannerPdfLayout.ContentTop;
            if (returnLine.Length > 0)
            {
                var returnLines = Wrap(returnLine, w - margin * 2 - 16, noteSize);
                var bandHeight = returnLines.Count * noteLeading + 10;
                page.Rectangle(margin, y - bandHeight, w - margin * 2, bandHeight, InstructionBg, true);
                page.Rectangle(margin, y - bandHeight, w - margin * 2, bandHeight, Grid, false);
                for (var l = 0; l < returnLines.Count; l++)
                    page.Text(returnLines[l], margin + 8, y - 17 - l * noteLeading, noteSize, PdfPalette.Ink);
                y -= bandHeight + 8;
            }
            var x = margin;
            const double headerHeight = ScannerPdfLayout.HeaderRowHeight;
            for (var i = 0; i < widths.Length; i++)
            {
                page.Rectangle(x, y - headerHeight, widths[i], headerHeight, HeaderBg, true);
                page.Rectangle(x, y - headerHeight, widths[i], headerHeight, Grid, false);
                page.Text(sheet.Headers[i], x + 5, y - 15, size, PdfPalette.Ink);
                x += widths[i];
            }
            y -= headerHeight;
        }

        NewPage();
        foreach (var row in sheet.Rows)
        {
            var entry = data.Entries.GetValueOrDefault(row.Id) ?? new ScannerEntry();
            var values = new[] { row.UserName, row.Gost, row.ScannerType,
                row.Position.Length > 0 ? row.Position : entry.Position,
                entry.HandoverTo, entry.ScannerNumber, entry.Comments };
            var lines = values.Select((value, i) => Wrap(value, widths[i] - 12, size)).ToArray();
            var printedNote = string.IsNullOrEmpty(row.Note) ? [] : Wrap(row.Note, widths[6] - 12, size);
            var noteHeight = printedNote.Count * leading;
            bool IsField(int col) => col >= 4 || col == 3 && row.Position.Length == 0;
            // Editable cells need the height their field appearance uses, not the height
            // of the same lines drawn straight onto the page.
            double CellHeight(int col) => IsField(col)
                ? PdfFormAppearance.MultilineHeight(font, size, lines[col].Count) + 6 + (col == 6 ? noteHeight : 0)
                : lines[col].Count * leading + 12;
            var height = Math.Max(28, Enumerable.Range(0, widths.Length).Max(CellHeight));
            if (y - height < bottom) NewPage();
            if (y - height < bottom)
                throw new ScannerValidationException($"{row.UserName}: reduce the number of lines in Comments to fit the PDF.");
            var x = margin;
            for (var col = 0; col < widths.Length; col++)
            {
                if (col == 2 && row.ScannerType == "Two piece")
                    page.Rectangle(x, y - height, widths[col], height, TwoPieceBg, true);
                page.Rectangle(x, y - height, widths[col], height, Grid, false);
                if (IsField(col))
                {
                    var reserved = col == 6 ? noteHeight : 0;
                    if (col == 6)
                    {
                        for (var l = 0; l < printedNote.Count; l++)
                            page.Text(printedNote[l], x + 6, y - 12 - l * leading, size, PdfPalette.Muted);
                    }
                    var suffix = col switch { 3 => "position", 4 => "handoverTo", 5 => "scannerNumber", _ => "comments" };
                    doc.WrappedTextField(page, $"{row.Id}_{suffix}", values[col], string.Join('\n', lines[col]),
                        x + 3, y - height + 3, widths[col] - 6, height - 6 - reserved, size);
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
            var lines = Wrap(instruction.Text, w - margin * 2 - 16, noteSize);
            var height = Math.Max(24, lines.Count * noteLeading + 10);
            if (y - height < bottom) NewPage();
            page.Rectangle(margin, y - height, w - margin * 2, height, InstructionBg, true);
            page.Rectangle(margin, y - height, w - margin * 2, height, Grid, false);
            for (var l = 0; l < lines.Count; l++)
                page.Text(lines[l], margin + 8, y - 17 - l * noteLeading, noteSize, PdfPalette.Ink);
            y -= height;
        }

        for (var i = 0; i < doc.Pages.Count; i++)
        {
            const double footer = ScannerPdfLayout.FooterBaseline;
            doc.Pages[i].Text($"Source: {version.SourceFile} / {sheet.SourceSheet}", margin, footer, 8, PdfPalette.Muted);
            doc.Pages[i].Text($"Page {i + 1} of {doc.Pages.Count}", w - margin - 80, footer, size, PdfPalette.Muted);
        }
        return doc.Save($"{sheet.Title} - {data.Date}", $"{version.Label} scanner handover form");
    }
}
