using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Tokens;

namespace UpsChecklist.Tests;

internal static class PdfTestHelpers
{
    public static AcroForm RequireForm(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        Assert.True(pdf.TryGetForm(out var form) && form is not null, "PDF has no AcroForm.");
        return form;
    }

    public static string? TextFieldValue(AcroForm form, string name) =>
        form.Fields.FirstOrDefault(f => f.Information.PartialName == name) is AcroTextField text
            ? text.Value
            : null;

    public static bool CheckboxChecked(AcroForm form, string name)
    {
        var field = form.Fields.FirstOrDefault(f => f.Information.PartialName == name);
        return field switch
        {
            AcroCheckboxField box => box.IsChecked,
            AcroCheckboxesField => FieldNameValueIsOn(field),
            _ => false,
        };
    }

    private static bool FieldNameValueIsOn(AcroFieldBase field)
    {
        if (!field.Dictionary.TryGet(NameToken.V, out var token))
            return false;

        var value = token.ToString();
        return value is "/Yes" or "Yes" or "/On" or "On";
    }

    public static bool HasField(AcroForm form, string name) =>
        form.Fields.Any(f => f.Information.PartialName == name);

    public static int FieldCount(AcroForm form, string name) =>
        form.Fields.Count(f => f.Information.PartialName == name);

    public static bool HasImage(byte[] bytes, int width, int height)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.GetPages().SelectMany(p => p.GetImages())
            .Any(i => i.WidthInSamples == width && i.HeightInSamples == height);
    }

    public static bool HasAnyImage(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.GetPages().Any(p => p.GetImages().Any());
    }

    public static int ImageCount(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.GetPages().Sum(p => p.GetImages().Count());
    }

    public static int PageCount(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.NumberOfPages;
    }

    public static string Title(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.Information.Title ?? "";
    }

    public static string RawAscii(byte[] bytes) =>
        System.Text.Encoding.ASCII.GetString(bytes);

    public static bool TextFieldDefaultAppearanceUsesWhiteInk(byte[] bytes) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            RawAscii(bytes),
            @"/DA\(/(?:Helv|Helvetica)[^)]*1 1 1 rg\)");

    public static bool ContainsInvalidArtifactBdc(byte[] bytes) =>
        RawAscii(bytes).Contains("/Artifact BDC", StringComparison.Ordinal);

    public static bool ReferencesZaDbFont(byte[] bytes) =>
        RawAscii(bytes).Contains("ZaDb", StringComparison.Ordinal);

    public static IReadOnlyList<(int Page, int TextLength, int ImageCount)> PageSummaries(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.GetPages()
            .Select((page, index) => (Page: index + 1, TextLength: page.Text.Length, ImageCount: page.GetImages().Count()))
            .ToList();
    }

    public static string AllText(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return string.Join('\n', pdf.GetPages().Select(page => page.Text));
    }

    /// <summary>Text a form field actually draws, with the box it is clipped to.</summary>
    public sealed record FieldAppearance(double ClipBottom, double ClipTop, IReadOnlyList<(double Baseline, int Glyphs)> Lines)
    {
        public int TotalGlyphs => Lines.Sum(line => line.Glyphs);
        public IEnumerable<(double Baseline, int Glyphs)> DrawnLines => Lines.Where(line => line.Glyphs > 0);
    }

    private static readonly System.Text.RegularExpressions.Regex AppearanceStream = new(
        @"/Subtype /Form /FormType 1 /BBox \[[\d. ]+\][\s\S]*?/Length (\d+) >>\nstream\n");
    private static readonly System.Text.RegularExpressions.Regex ClipBox = new(
        @"([-\d.]+) ([-\d.]+) ([-\d.]+) ([-\d.]+) re W n");
    private static readonly System.Text.RegularExpressions.Regex AppearanceLine = new(
        @"1 0 0 1 ([-\d.]+) ([-\d.]+) Tm\s*<([0-9A-Fa-f]*)> Tj");

    public static IReadOnlyList<FieldAppearance> FieldAppearances(byte[] bytes)
    {
        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        var appearances = new List<FieldAppearance>();
        foreach (System.Text.RegularExpressions.Match stream in AppearanceStream.Matches(raw))
        {
            var content = Inflate(bytes, stream.Index + stream.Length, int.Parse(stream.Groups[1].Value));
            var clip = ClipBox.Match(content);
            if (!clip.Success)
                continue; // Checkbox appearances draw no text.
            var bottom = Double(clip.Groups[2]);
            var lines = AppearanceLine.Matches(content)
                .Select(line => (Baseline: Double(line.Groups[2]), Glyphs: line.Groups[3].Length / 4))
                .ToList();
            appearances.Add(new FieldAppearance(bottom, bottom + Double(clip.Groups[4]), lines));
        }
        return appearances;
    }

    private static double Double(System.Text.RegularExpressions.Group group) =>
        double.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture);

    private static string Inflate(byte[] bytes, int offset, int length)
    {
        using var compressed = new MemoryStream(bytes, offset, length);
        using var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(zlib, System.Text.Encoding.Latin1);
        return reader.ReadToEnd();
    }
}
