using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using UpsChecklist.Core.Models;
using TextPosition = Aspose.Pdf.Text.Position;
using ChecklistPosition = UpsChecklist.Core.Models.Position;

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

        using var document = Document.Create();
        document.EmbedStandardFonts = true;
        document.Info.Title = $"Sunrise {position.Label} checklist";
        document.Info.Subject = $"Prototype from source worksheet: {position.SourceSheet}";
        document.Info.Creator = "Position checklist prototype";

        var font = LoadFont();
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

        void Paragraph(string value, double size = 10, Color? color = null)
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
                var chunkHeight = Math.Max(58, chunk.Count * 15 + 20);
                TextFieldBlock(key + suffix, label + (offset == 0 ? "" : " (continued)"), string.Join('\n', chunk), Cw, chunkHeight, true);
            }
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

        if (!string.IsNullOrEmpty(data.EvidencePhoto))
        {
            Ensure(180 + 34 + 100);
            var maxHeight = Math.Min(360, ctx.Y - 55 - 34 - 100);
            ctx.DrawText("Evidence photo", M, ctx.Y - 11, 10, PdfPalette.Brown);
            ctx.Y -= 20;
            var size = ctx.DrawImageDataUrl(data.EvidencePhoto, M + Cw / 2, ctx.Y, Cw, maxHeight, center: true);
            ctx.Y -= size.Height + 14;
        }

        if (!string.IsNullOrEmpty(data.Drawn))
        {
            Ensure(85);
            ctx.DrawText("Drawn signature (not identity verified)", M, ctx.Y - 11, 10, PdfPalette.Brown);
            ctx.Y -= 20;
            ctx.DrawBorder(M, ctx.Y - 53, Cw, 53, PdfPalette.Line);
            ctx.DrawImageDataUrl(data.Drawn, M + 8, ctx.Y - 48, Cw - 16, 43);
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
            var page = document.Pages[index + 1];
            DrawHorizontalLine(page, M, 43, Cw, 0.6, PdfPalette.Line);
            AppendText(page, font, $"Prototype | Source: {position.SourceSheet}", M, 29, 8, PdfPalette.Muted);
            AppendText(page, font, $"Page {index + 1} of {document.Pages.Count}", W - M - 62, 29, 8, PdfPalette.Muted);
        }

        PdfFormAppearance.Apply(document, font);

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private Font LoadFont()
    {
        if (!File.Exists(_fontPath))
            return FontRepository.FindFont("Helvetica");

        var font = FontRepository.OpenFont(_fontPath);
        font.IsEmbedded = true;
        return font;
    }

    private static List<string> Wrapped(LayoutContext ctx, string value, double width, double size)
    {
        var result = new List<string>();
        foreach (var paragraph in value.Replace("\r", "").Split('\n'))
        {
            var current = "";
            foreach (var ch in paragraph)
            {
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

    private static void DrawHorizontalLine(Page page, double x, double y, double width, double thickness, Color color) =>
        PdfDrawing.HorizontalLine(page, x, y, width, thickness, color);

    private static void AppendText(Page page, Font font, string text, double x, double baseline, float size, Color color)
    {
        page.Paragraphs.Add(new TextFragment(text)
        {
            TextState =
            {
                Font = font,
                FontSize = size,
                ForegroundColor = color,
                RenderingMode = TextRenderingMode.FillText,
            },
            Position = new TextPosition(x, baseline),
        });
    }

    private static (double Width, double Height) MeasureImage(string path, double maxWidth, double maxHeight)
    {
        using var image = System.Drawing.Image.FromFile(path);
        var width = image.Width * 72.0 / image.HorizontalResolution;
        var height = image.Height * 72.0 / image.VerticalResolution;
        var scale = Math.Min(1, Math.Min(maxWidth / width, maxHeight / height));
        return (width * scale, height * scale);
    }

    private sealed class LayoutContext(Document document, Font font)
    {
        public double Y { get; set; }
        private Page _page = null!;
        private int _pageIndex;
        private TextBoxField? _nameField;
        private TextBoxField? _dateField;

        public void BeginPage()
        {
            _page = document.Pages.Add();
            _pageIndex = document.Pages.Count;
            Y = 0;
        }

        public void QueueHeaderField(string name, string value, double x, double bottom, double width, double height)
        {
            var rect = new Rectangle(x, bottom, x + width, bottom + height);
            if (name == "name" && _nameField is not null)
            {
                document.Form.Add(_nameField, _pageIndex);
                return;
            }

            if (name == "date" && _dateField is not null)
            {
                document.Form.Add(_dateField, _pageIndex);
                return;
            }

            var field = new TextBoxField(_page, rect)
            {
                PartialName = name,
                Value = value,
            };
            PrepareTextField(field, value, width, multiline: false, maxFontSize: 10, fitWidth: name == "name" ? 300 : width - 10);
            AddField(field);
            if (name == "name")
                _nameField = field;
            else if (name == "date")
                _dateField = field;
        }

        public void DrawText(string text, double x, double baseline, double size, Color color)
        {
            _page.Paragraphs.Add(new TextFragment(text)
            {
                TextState =
                {
                    Font = font,
                    FontSize = (float)size,
                    ForegroundColor = color,
                    RenderingMode = TextRenderingMode.FillText,
                },
                Position = new TextPosition(x, baseline),
            });
        }

        public void DrawFilledRect(double x, double bottom, double width, double height, Color color) =>
            PdfDrawing.FillRect(_page, x, bottom, width, height, color);

        public void DrawBorder(double x, double bottom, double width, double height, Color color) =>
            PdfDrawing.StrokeRect(_page, x, bottom, width, height, color);

        public void DrawImageFile(string path, double x, double bottom, double width, double height)
        {
            _page.AddImage(path, new Rectangle(x, bottom, x + width, bottom + height));
        }

        public (double Width, double Height) DrawImageDataUrl(string dataUrl, double x, double topY, double maxWidth, double maxHeight, bool center = false)
        {
            var comma = dataUrl.IndexOf(',');
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            using var image = System.Drawing.Image.FromStream(new MemoryStream(bytes));
            var width = image.Width * 72.0 / image.HorizontalResolution;
            var height = image.Height * 72.0 / image.VerticalResolution;
            var scale = Math.Min(1, Math.Min(maxWidth / width, maxHeight / height));
            var w = width * scale;
            var h = height * scale;
            var left = center ? x - w / 2 : x;
            var bottom = topY - h;
            using var stream = new MemoryStream(bytes);
            _page.AddImage(stream, new Rectangle(left, bottom, left + w, bottom + h));
            return (w, h);
        }

        public void QueueTextField(string name, string value, double x, double bottom, double width, double height, bool multiline = false)
        {
            var field = new TextBoxField(_page, new Rectangle(x, bottom, x + width, bottom + height))
            {
                PartialName = name,
                Value = value,
                Multiline = multiline,
            };
            PrepareTextField(field, value, width, multiline);
            AddField(field);
        }

        public void QueueCheckBox(string name, bool isChecked, double x, double bottom, double width, double height)
        {
            var field = new CheckboxField(_page, new Rectangle(x, bottom, x + width, bottom + height))
            {
                PartialName = name,
                Checked = isChecked,
            };
            AddField(field);
        }

        public double Measure(string text, double size) => font.MeasureString(text, (float)size);

        private void AddField(Field field) => document.Form.Add(field, _pageIndex);

        private void PrepareTextField(TextBoxField field, string value, double width, bool multiline, double maxFontSize = 11, double fitWidth = 0)
        {
            var fit = fitWidth > 0 ? fitWidth : width - 10;
            var fontSize = multiline ? 10 : Math.Min(maxFontSize, fit / Math.Max(1, font.MeasureString(value, 1f)));
            field.DefaultAppearance = new DefaultAppearance(font, fontSize, System.Drawing.Color.Black);
        }
    }
}
