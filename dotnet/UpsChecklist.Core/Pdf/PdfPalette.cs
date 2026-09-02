using Aspose.Pdf;
using SystemDrawingColor = System.Drawing.Color;

namespace UpsChecklist.Core.Pdf;

internal static class PdfPalette
{
    // pdf-lib uses 0..1 RGB; Aspose Color.FromRgb(double, double, double) expects the same range.
    public static readonly Color Brown = Color.FromRgb(0.28, 0.17, 0.10);
    public static readonly Color Ink = Color.FromRgb(0.12, 0.12, 0.12);
    public static readonly Color Muted = Color.FromRgb(0.35, 0.35, 0.35);
    public static readonly Color Line = Color.FromRgb(0.72, 0.74, 0.76);
    public static readonly Color SectionBg = Color.FromRgb(0.94, 0.93, 0.91);
    public static readonly Color FieldFill = Color.FromRgb(1, 1, 1);

    // System.Drawing colors for DefaultAppearance — use named constants; byte RGB values
    // are misread as 0..1 by Aspose and clamp to white (same bug as Color.FromRgb bytes).
    public static SystemDrawingColor InkDrawing => SystemDrawingColor.Black;
    public static SystemDrawingColor MutedDrawing => SystemDrawingColor.DarkGray;
    public static SystemDrawingColor FieldBorderDrawing => SystemDrawingColor.LightGray;
}
