namespace UpsChecklist.Core.Pdf;

/// <summary>Scanner page geometry, shared by the PDF writer and the independent PDF checks.</summary>
public static class ScannerPdfLayout
{
    public const double PageWidth = 841.89;
    public const double PageHeight = 595.28;
    public const double Margin = 28;
    /// <summary>Top of the column headers; nothing editable is drawn above it.</summary>
    public const double ContentTop = PageHeight - 78;
    /// <summary>Rows and instruction bands never cross this line.</summary>
    public const double ContentBottom = 48;
    /// <summary>Baseline of the source and page-number line.</summary>
    public const double FooterBaseline = 22;
    public const double HeaderRowHeight = 22;
    public const double FontSize = 9;
    public const double NoteFontSize = 10;
}
