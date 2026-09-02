using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;

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

    public static bool CheckboxChecked(AcroForm form, string name) =>
        form.Fields.FirstOrDefault(f => f.Information.PartialName == name) is AcroCheckboxField box && box.IsChecked;

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
        return pdf.GetPages().Any(p => p.GetImages().Any()) ||
               RawAscii(bytes).Contains("/Subtype/Image", StringComparison.Ordinal);
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
}
