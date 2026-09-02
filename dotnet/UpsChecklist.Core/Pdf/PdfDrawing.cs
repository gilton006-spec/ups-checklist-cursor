using Aspose.Pdf;
using Aspose.Pdf.Operators;

namespace UpsChecklist.Core.Pdf;

internal static class PdfDrawing
{
    public static void FillRect(Page page, double x, double bottom, double width, double height, Color color)
    {
        var (r, g, b) = ToRgb(color);
        page.Contents.Add(new SetRGBColor(r, g, b));
        page.Contents.Add(new Re(x, bottom, width, height));
        page.Contents.Add(new Fill());
    }

    public static void StrokeRect(Page page, double x, double bottom, double width, double height, Color color)
    {
        var (r, g, b) = ToRgb(color);
        page.Contents.Add(new SetRGBColorStroke(r, g, b));
        page.Contents.Add(new Re(x, bottom, width, height));
        page.Contents.Add(new ClosePathStroke());
    }

    public static void HorizontalLine(Page page, double x, double y, double width, double thickness, Color color) =>
        FillRect(page, x, y, width, thickness, color);

    private static (double R, double G, double B) ToRgb(Color color)
    {
        // Aspose stores FromRgb(d,d,d) as 0..255 byte channels on Color.R/G/B.
        if (color.R <= 1 && color.G <= 1 && color.B <= 1)
            return (color.R, color.G, color.B);

        return (color.R / 255.0, color.G / 255.0, color.B / 255.0);
    }
}
