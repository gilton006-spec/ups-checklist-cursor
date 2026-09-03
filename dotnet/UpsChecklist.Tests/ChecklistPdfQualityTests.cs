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

        Assert.False(PdfTestHelpers.TextFieldDefaultAppearanceUsesWhiteInk(bytes));
        Assert.False(PdfTestHelpers.ContainsInvalidArtifactBdc(bytes));
        Assert.False(PdfTestHelpers.ReferencesZaDbFont(bytes));

        var pages = PdfTestHelpers.PageSummaries(bytes);
        Assert.Equal(2, pages.Count);
        Assert.All(pages, p => Assert.True(p.TextLength > 100, $"Page {p.Page} has too little text."));
        Assert.True(pages[0].ImageCount >= 2,
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
    [InlineData("ps1-ps2-monday")]
    [InlineData("ps1")]
    [InlineData("ps2")]
    [InlineData("pd1")]
    [InlineData("pd2")]
    [InlineData("pd3")]
    [InlineData("pd4")]
    [InlineData("smalls")]
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

    [Fact]
    public void Long_remarks_are_drawn_inside_their_field_boxes()
    {
        var position = ChecklistPositions.Get("pd3")!;
        var remark = string.Join(' ', Enumerable.Repeat("Followed up with the team leader about the jam at the merge.", 12));
        var data = new ChecklistSubmission
        {
            PositionId = position.Id,
            Date = "2026-09-02",
            Name = "Quality Check",
            Count = "2",
            Remarks = remark,
            SectionRemarks = new Dictionary<string, string> { ["sls1"] = remark, ["recirculation"] = remark },
            Signature = "Quality Check",
        };

        var appearances = PdfTestHelpers.FieldAppearances(new ChecklistPdfCreator().Create(data));

        Assert.NotEmpty(appearances);
        foreach (var appearance in appearances)
            foreach (var (baseline, _) in appearance.DrawnLines)
                Assert.InRange(baseline, appearance.ClipBottom, appearance.ClipTop);
        var expected = remark.Replace(" ", "").Length;
        Assert.Contains(appearances, appearance => appearance.TotalGlyphs >= expected);
    }

    [Theory]
    [InlineData("nl-NL")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Pdf_serialization_is_independent_of_request_culture(string culture)
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        var data = new ChecklistSubmission
        {
            PositionId = "pd3", Name = "Zoë Müller", Date = "2026-09-02",
            Count = "7", Remarks = "Café checked.", Signature = "Zoë Müller",
            Checks = new Dictionary<string, bool> { ["b8"] = true, ["b10"] = false },
        };
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var expected = new ChecklistPdfCreator().Create(data);
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            var actual = new ChecklistPdfCreator().Create(data);
            Assert.Equal(expected, actual);
            Assert.Equal(culture, System.Globalization.CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}
