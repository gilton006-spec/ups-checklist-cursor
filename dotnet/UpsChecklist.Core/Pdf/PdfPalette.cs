namespace UpsChecklist.Core.Pdf;

internal readonly record struct PdfColor(double R, double G, double B)
{
    public string Fill => $"{PdfSyntax.Number(R)} {PdfSyntax.Number(G)} {PdfSyntax.Number(B)} rg";
    public string Stroke => $"{PdfSyntax.Number(R)} {PdfSyntax.Number(G)} {PdfSyntax.Number(B)} RG";
}

internal static class PdfPalette
{
    public static readonly PdfColor Brown = new(0.28, 0.17, 0.10);
    public static readonly PdfColor Ink = new(0.12, 0.12, 0.12);
    public static readonly PdfColor Muted = new(0.35, 0.35, 0.35);
    public static readonly PdfColor Line = new(0.72, 0.74, 0.76);
    public static readonly PdfColor SectionBg = new(0.94, 0.93, 0.91);
}
