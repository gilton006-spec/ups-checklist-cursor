using System.Text;
using UpsChecklist.Core.Models;

namespace UpsChecklist.Core.Pdf;

public sealed class ChecklistPdfCreator
{
    private const double W = 595.28;
    private const double H = 841.89;
    private const double M = 36;
    private static readonly double Cw = W - M * 2;

    private readonly string _workbookDirectory;
    private readonly string _fontPath;

    public ChecklistPdfCreator(string? workbookDirectory = null, string? fontPath = null)
    {
        _workbookDirectory = workbookDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Workbook");
        _fontPath = fontPath
            ?? Path.Combine(AppContext.BaseDirectory, "Assets", "checklist-font.ttf");
    }

    public byte[] Create(ChecklistSubmission data)
    {
        var position = ChecklistPositions.Get(data.PositionId)
            ?? throw new InvalidOperationException("Unknown checklist position.");

        var font = new ChecklistFont(_fontPath);
        var document = new PdfDocumentWriter(font);
        var ctx = new LayoutContext(document, font);

        void NewPage()
        {
            ctx.BeginPage();
            ctx.DrawFilledRect(0, H - 12, W, 12, PdfPalette.Brown);
            ctx.DrawText("SUNRISE  |  CHECKLIST JAMB. / SING.", M, H - 38, 10, PdfPalette.Brown);
            ctx.DrawText(position.Title, M, H - 62, 19, PdfPalette.Brown);
            ctx.DrawText(position.ScheduleLabel ?? "Position checklist", M, H - 80, 9, PdfPalette.Muted);
            ctx.DrawText("Name:", M, H - 105, 10, PdfPalette.Ink);
            ctx.QueueHeaderField("name", data.Name, M + 37, H - 112, 307, 22);
            ctx.DrawText("Date:", 397, H - 105, 10, PdfPalette.Ink);
            ctx.QueueHeaderField("date", data.Date, 429, H - 112, 130, 22);
            ctx.Y = H - 136;
        }

        void Ensure(double height)
        {
            if (ctx.Y - height < 55)
                NewPage();
        }

        void Paragraph(string value, double size = 10, PdfColor? color = null)
        {
            foreach (var lineText in Wrapped(ctx, value, Cw, size))
            {
                Ensure(size + 5);
                ctx.DrawText(lineText, M, ctx.Y - size, size, color ?? PdfPalette.Ink);
                ctx.Y -= size + 5;
            }

            ctx.Y -= 5;
        }

        void SectionTitle(string title)
        {
            Ensure(65);
            ctx.DrawFilledRect(M, ctx.Y - 25, Cw, 25, PdfPalette.SectionBg);
            ctx.DrawText(title, M + 9, ctx.Y - 17, 12, PdfPalette.Brown);
            ctx.Y -= 36;
        }

        void TextFieldBlock(string key, string label, string value, double width, double height, bool multiline)
        {
            Ensure(height + 33);
            ctx.DrawText(label, M, ctx.Y - 11, 10, PdfPalette.Brown);
            ctx.Y -= 20;
            ctx.QueueTextField(key, value, M, ctx.Y - height, width, height, multiline);
            ctx.Y -= height + 14;
        }

        void Remarks(string key, string label, string value)
        {
            var lines = Wrapped(ctx, value, Cw - 16, 10);
            const int chunkSize = 26;
            for (var offset = 0; offset < lines.Count; offset += chunkSize)
            {
                var chunk = lines.Skip(offset).Take(chunkSize).ToList();
                var suffix = offset == 0 ? "" : $"_continued_{offset / chunkSize}";
                // Never reserve less than the field appearance needs, or the last remark line is clipped.
                var chunkHeight = Math.Max(Math.Max(58, chunk.Count * PdfFormAppearance.LineHeight(10) + 20),
                    PdfFormAppearance.MultilineHeight(font, 10, chunk.Count) + 6);
                TextFieldBlock(key + suffix, label + (offset == 0 ? "" : " (continued)"), string.Join('\n', chunk), Cw, chunkHeight, true);
            }
        }

        void EvidenceBlock(string label, string photo, double reserveBelow = 0)
        {
            if (string.IsNullOrEmpty(photo))
                return;

            Ensure(180 + 34 + reserveBelow);
            var maxHeight = Math.Min(360, Math.Max(80, ctx.Y - 55 - 34 - reserveBelow));
            ctx.DrawText(label, M, ctx.Y - 11, 10, PdfPalette.Brown);
            ctx.Y -= 20;
            var size = ctx.DrawImageDataUrl(photo, M + Cw / 2, ctx.Y, Cw, maxHeight, center: true);
            ctx.Y -= size.Height + 14;
        }

        NewPage();
        Paragraph("PROTOTYPE FOR REVIEW. Not UPS approved. Downloading does not submit this report.", 9, PdfPalette.Muted);
        var warning = ChecklistPositions.ScheduleWarning(position, data.Date);
        if (warning is not null)
            Paragraph(warning, 9, PdfPalette.Brown);

        foreach (var section in position.Sections)
        {
            var images = (section.Images ?? [])
                .Select(reference => (reference, path: Path.Combine(_workbookDirectory, reference.File)))
                .ToList();
            var firstSize = images.Count > 0 ? MeasureImage(images[0].path, Cw, 165) : ((double Width, double Height)?)null;
            var noteHeight = section.Note is null ? 0 : Wrapped(ctx, section.Note, Cw, 9).Count * 14 + 5;
            Ensure(36 + noteHeight + (firstSize?.Height ?? 0) + 33 + 40);
            SectionTitle(section.Title);
            if (section.Note is not null)
                Paragraph(section.Note, 9, PdfPalette.Brown);

            foreach (var (reference, path) in images)
            {
                var size = MeasureImage(path, Cw, 165);
                Ensure(size.Height + 48);
                ctx.DrawText(reference.Label, M, ctx.Y - 10, 9, PdfPalette.Muted);
                ctx.Y -= 19;
                ctx.DrawImageFile(path, M + (Cw - size.Width) / 2, ctx.Y - size.Height, size.Width, size.Height);
                ctx.Y -= size.Height + 14;
            }

            foreach (var item in section.Items)
            {
                var lines = Wrapped(ctx, item.Text, Cw - 30, 10.5);
                var notes = item.Note is null ? [] : Wrapped(ctx, item.Note, Cw - 30, 9);
                var height = Math.Max(25, lines.Count * 14 + notes.Count * 13 + 10);
                Ensure(height);
                ctx.QueueCheckBox(item.Id, data.Checks.GetValueOrDefault(item.Id), M + 1, ctx.Y - 16, 14, 14);
                var baseline = ctx.Y - 12;
                foreach (var l in lines)
                {
                    ctx.DrawText(l, M + 26, baseline, 10.5, PdfPalette.Ink);
                    baseline -= 14;
                }

                foreach (var l in notes)
                {
                    ctx.DrawText(l, M + 26, baseline, 9, PdfPalette.Muted);
                    baseline -= 13;
                }

                ctx.Y -= height;
            }

            if (section.RemarksKey is not null)
            {
                var label = section.RemarksKey == "sls1" ? "SLS1 remarks" : "Recirculation remarks";
                Remarks($"remarks_{section.RemarksKey}", label, data.SectionRemarks.GetValueOrDefault(section.RemarksKey) ?? "");
            }

            if (section.Id == "before")
                EvidenceBlock("Before-sort evidence photo", data.BeforeSortEvidencePhoto);

            ctx.Y -= 8;
        }

        TextFieldBlock("package_count", position.PackageLabel, data.Count, 170, 28, false);
        Remarks("remarks", "Other remarks / incomplete checks / actions / follow-up", data.Remarks);

        if (position.Handover is not null)
        {
            Paragraph("Original paper handover instruction:", 10, PdfPalette.Brown);
            Paragraph(position.Handover, 9);
            Paragraph("Confirm the digital handover process with your team leader. Downloading does not send the report.", 9, PdfPalette.Muted);
        }

        EvidenceBlock("After-sort evidence photo", data.EvidencePhoto, reserveBelow: 100);

        if (!string.IsNullOrEmpty(data.Drawn))
        {
            Ensure(85);
            ctx.DrawText("Drawn signature (not identity verified)", M, ctx.Y - 11, 10, PdfPalette.Brown);
            ctx.Y -= 20;
            ctx.DrawBorder(M, ctx.Y - 53, Cw, 53, PdfPalette.Line);
            // DrawImageDataUrl takes a TOP coordinate; keep the image inside its box.
            ctx.DrawImageDataUrl(data.Drawn, M + 8, ctx.Y - 5, Cw - 16, 43);
            ctx.Y -= 67;
        }
        else
        {
            TextFieldBlock("signature", "Signature (not identity verified)", data.Signature, Cw, 53, false);
        }

        if (position.SourceNotes is { Count: > 0 })
        {
            SectionTitle("Source instructions to confirm");
            foreach (var note in position.SourceNotes)
                Paragraph(note, 9, PdfPalette.Muted);
        }

        for (var index = 0; index < document.Pages.Count; index++)
        {
            var page = document.Pages[index];
            page.Rectangle(M, 43, Cw, 0.6, PdfPalette.Line, fill: true);
            page.Text($"Prototype | Source: {position.SourceSheet}", M, 29, 8, PdfPalette.Muted);
            page.Text($"Page {index + 1} of {document.Pages.Count}", W - M - 62, 29, 8, PdfPalette.Muted);
        }

        return document.Save($"Sunrise {position.Label} checklist", $"Prototype from source worksheet: {position.SourceSheet}");
    }

    private static List<string> Wrapped(LayoutContext ctx, string value, double width, double size)
    {
        var result = new List<string>();
        foreach (var paragraph in value.Replace("\r", "").Replace("\t", "    ").Split('\n'))
        {
            var current = "";
            foreach (var rune in paragraph.EnumerateRunes())
            {
                var ch = rune.ToString();
                var next = current + ch;
                if (current.Length > 0 && ctx.Measure(next, size) > width)
                {
                    var lastSpace = current.LastIndexOf(' ');
                    if (lastSpace > current.Length * 0.5)
                    {
                        result.Add(current[..lastSpace]);
                        current = current[(lastSpace + 1)..] + ch;
                    }
                    else
                    {
                        result.Add(current);
                        current = ch.ToString();
                    }
                }
                else
                {
                    current = next;
                }
            }

            result.Add(current);
        }

        return result;
    }

    private static (double Width, double Height) MeasureImage(string path, double maxWidth, double maxHeight) =>
        PdfImage.Load(path).Fit(maxWidth, maxHeight);

    private sealed class LayoutContext(PdfDocumentWriter document, ChecklistFont font)
    {
        public double Y { get; set; }
        private PdfPageCanvas _page = null!;

        public void BeginPage()
        {
            _page = document.AddPage(W, H);
            Y = 0;
        }

        public void QueueHeaderField(string name, string value, double x, double bottom, double width, double height)
        {
            var size = Math.Min(10, (width - 10) / Math.Max(1, font.Measure(value, 1)));
            document.TextField(_page, name, value, x, bottom, width, height, false, size);
        }

        public void DrawText(string text, double x, double baseline, double size, PdfColor color) =>
            _page.Text(text, x, baseline, size, color);

        public void DrawFilledRect(double x, double bottom, double width, double height, PdfColor color) =>
            _page.Rectangle(x, bottom, width, height, color, fill: true);

        public void DrawBorder(double x, double bottom, double width, double height, PdfColor color) =>
            _page.Rectangle(x, bottom, width, height, color, fill: false);

        public void DrawImageFile(string path, double x, double bottom, double width, double height)
        {
            _page.Image(PdfImage.Load(path), x, bottom, width, height);
        }

        public (double Width, double Height) DrawImageDataUrl(string dataUrl, double x, double topY, double maxWidth, double maxHeight, bool center = false)
        {
            var comma = dataUrl.IndexOf(',');
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            var image = PdfImage.Read(bytes);
            var (w, h) = image.Fit(maxWidth, maxHeight);
            var left = center ? x - w / 2 : x;
            var bottom = topY - h;
            _page.Image(image, left, bottom, w, h);
            return (w, h);
        }

        public void QueueTextField(string name, string value, double x, double bottom, double width, double height, bool multiline = false)
        {
            var size = multiline ? 10 : Math.Min(11, (width - 10) / Math.Max(1, font.Measure(value, 1)));
            document.TextField(_page, name, value, x, bottom, width, height, multiline, size);
        }

        public void QueueCheckBox(string name, bool isChecked, double x, double bottom, double width, double height)
        {
            document.Checkbox(_page, name, isChecked, x, bottom, width, height);
        }

        public double Measure(string text, double size) => font.Measure(text, size);
    }
}
