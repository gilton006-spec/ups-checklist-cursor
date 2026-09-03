using System.Text;

namespace UpsChecklist.Core.Pdf;

internal static class PdfFormAppearance
{
    // Text is clipped to this inset and the first baseline sits TopPadding below the top
    // edge. Callers must lay out with LineHeight and MultilineHeight, or the last lines
    // of a value fall outside the clip box and disappear from the rendered page.
    private const double Clip = 3;
    private const double TopPadding = 5;

    public static double LineHeight(double size) => size + 5;

    public static double MultilineHeight(ChecklistFont font, double size, int lines) =>
        TopPadding + font.Ascent * size / 1000
        + LineHeight(size) * (Math.Max(1, lines) - 1)
        + Math.Abs(font.Descent) * size / 1000 + Clip;

    private static StringBuilder Border(double width, double height) =>
        new StringBuilder("q\n1 1 1 rg\n0.72 0.74 0.76 RG\n0.8 w\n")
            .Append(PdfSyntax.Numbers(0.4, 0.4, width - 0.8, height - 0.8)).Append(" re B\n");

    public static string Text(ChecklistFont font, string value, double width, double height, double size, bool multiline)
    {
        var content = Border(width, height);
        content.Append(PdfSyntax.Numbers(Clip, Clip, width - Clip * 2, height - Clip * 2)).Append(" re W n\n")
            .Append(PdfPalette.Ink.Fill).Append("\nBT\n/F0 ").Append(PdfSyntax.Number(size)).Append(" Tf\n");
        var baseline = multiline ? height - TopPadding - font.Ascent * size / 1000
            : (height - (font.Ascent + font.Descent) * size / 1000) / 2;
        foreach (var line in value.Replace("\r", "").Split('\n'))
        {
            content.Append("1 0 0 1 ").Append(PdfSyntax.Numbers(4, baseline)).Append(" Tm\n")
                .Append(font.Encode(line)).Append(" Tj\n");
            baseline -= LineHeight(size);
        }
        return content.Append("ET\nQ\n").ToString();
    }

    public static string Checkbox(double width, double height, bool isChecked)
    {
        var content = Border(width, height);
        if (isChecked)
            content.Append("0.12 0.12 0.12 RG\n1.8 w\n1 J\n1 j\n")
                .Append(PdfSyntax.Numbers(width * 0.2, height * 0.49)).Append(" m\n")
                .Append(PdfSyntax.Numbers(width * 0.42, height * 0.26)).Append(" l\n")
                .Append(PdfSyntax.Numbers(width * 0.81, height * 0.77)).Append(" l S\n");
        return content.Append("Q\n").ToString();
    }
}
