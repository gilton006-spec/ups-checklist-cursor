using UpsChecklist.Core;
using UpsChecklist.Core.Models;
using UpsChecklist.Core.Pdf;

namespace UpsChecklist.Tests;

public class ChecklistPdfQualityTests
{
    [Fact]
    public void Pd3_regression_pdf_has_visible_text_appearances_and_valid_structure()
    {
        var position = ChecklistPositions.Get("pd3")!;
        var data = new ChecklistSubmission
        {
            PositionId = position.Id,
            Date = "2026-09-02",
            Name = "twtst",
            Checks = ChecklistPositions.AllItems(position).ToDictionary(i => i.Id, _ => true),
            Count = "3",
            Remarks = "test remark",
            Signature = "twtst",
        };

        var bytes = new ChecklistPdfCreator().Create(data);

        Assert.True(bytes.Length > 400_000, $"PDF too small ({bytes.Length} bytes); workbook={Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "Workbook"))}");

        Assert.False(PdfTestHelpers.TextFieldDefaultAppearanceUsesWhiteInk(bytes));
        Assert.False(PdfTestHelpers.ContainsInvalidArtifactBdc(bytes));
        Assert.False(PdfTestHelpers.ReferencesZaDbFont(bytes));

        var pages = PdfTestHelpers.PageSummaries(bytes);
        Assert.Equal(2, pages.Count);
        Assert.All(pages, p => Assert.True(p.TextLength > 100, $"Page {p.Page} has too little text."));
        Assert.True(pages[0].ImageCount >= 2 || PdfTestHelpers.RawAscii(bytes).Contains("/Subtype/Image"),
            $"Page 1 image markers missing (count={pages[0].ImageCount}, size={bytes.Length}).");

        var form = PdfTestHelpers.RequireForm(bytes);
        Assert.Equal("twtst", PdfTestHelpers.TextFieldValue(form, "name"));
        Assert.Equal("2026-09-02", PdfTestHelpers.TextFieldValue(form, "date"));
        Assert.Equal("3", PdfTestHelpers.TextFieldValue(form, "package_count"));
        Assert.Equal("test remark", PdfTestHelpers.TextFieldValue(form, "remarks"));
        Assert.Equal("twtst", PdfTestHelpers.TextFieldValue(form, "signature"));
        foreach (var item in ChecklistPositions.AllItems(position))
            Assert.True(PdfTestHelpers.CheckboxChecked(form, item.Id));
    }

    [Theory]
    [InlineData("ps1")]
    [InlineData("pd3")]
    [InlineData("matrix")]
    public void Every_page_of_each_position_has_text_and_valid_form_colours(string positionId)
    {
        var position = ChecklistPositions.Get(positionId)!;
        var data = new ChecklistSubmission
        {
            PositionId = position.Id,
            Date = position.Schedule == "monday" ? "2026-09-07" : "2026-09-02",
            Name = "Quality Check",
            Checks = ChecklistPositions.AllItems(position)
                .Select((item, index) => (item.Id, index % 2 == 0))
                .ToDictionary(x => x.Id, x => x.Item2),
            Count = "4",
            Remarks = "Checked every page.",
            Signature = "Quality Check",
        };

        var bytes = new ChecklistPdfCreator().Create(data);

        Assert.False(PdfTestHelpers.TextFieldDefaultAppearanceUsesWhiteInk(bytes));
        Assert.False(PdfTestHelpers.ContainsInvalidArtifactBdc(bytes));
        Assert.False(PdfTestHelpers.ReferencesZaDbFont(bytes));

        foreach (var (page, textLength, _) in PdfTestHelpers.PageSummaries(bytes))
            Assert.True(textLength > 80, $"{positionId} page {page} is missing static text.");
    }
}
