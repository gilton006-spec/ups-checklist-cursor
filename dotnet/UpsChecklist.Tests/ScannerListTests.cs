using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using UpsChecklist.Core.Pdf;
using UpsChecklist.Core.Scanners;

namespace UpsChecklist.Tests;

public sealed class ScannerListTests(ChecklistWebApplicationFactory factory) : IClassFixture<ChecklistWebApplicationFactory>
{
    public static IEnumerable<object[]> Lists() => ScannerLists.All.SelectMany(v => v.Sheets.Select(s =>
        new object[] { v.Id, s.Id, v.Id == "monday" ? "2026-09-07" : "2026-09-08" }));

    private async Task<HttpClient> ClientWithToken()
    {
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/Scanners");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "Scanner page must issue an anti-forgery token without a login.");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value));
        return client;
    }

    [Fact]
    public void Date_selects_monday_or_weekday_list_and_rejects_weekend()
    {
        Assert.Equal("monday", ScannerLists.ForDate(new DateOnly(2026, 9, 7))!.Id);
        Assert.Equal("tuesday-friday", ScannerLists.ForDate(new DateOnly(2026, 9, 8))!.Id);
        Assert.Equal("tuesday-friday", ScannerLists.ForDate(new DateOnly(2026, 9, 11))!.Id);
        Assert.Null(ScannerLists.ForDate(new DateOnly(2026, 9, 12)));
        Assert.Null(ScannerLists.ForDate(new DateOnly(2026, 9, 13)));
        Assert.Equal(80, ScannerLists.All.SelectMany(v => v.Sheets).Sum(s => s.Rows.Count));
    }

    [Fact]
    public async Task Scanner_page_and_jambreakers_link_to_each_other_and_keep_all_lists()
    {
        using var client = factory.CreateClient();
        var home = await client.GetStringAsync("/");
        var response = await client.GetAsync("/Scanners");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/Scanners\"", home);
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("<title>Sunrise Scanner Lists</title>", html);
        Assert.Contains("Scanner list pd1 2 of 2", html);
        Assert.Contains("Stockx 03", html);
        Assert.Contains("All two piece with a safety cover", html);
        Assert.Contains("Tuesday / Friday", html);
        Assert.DoesNotContain("Leave the scanner</strong>", html);
        Assert.DoesNotContain("Everyone must leave the scanner", html);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("/js/scanners.js")]
    [InlineData("/js/scanner-state.mjs")]
    [InlineData("/css/scanners.css")]
    [InlineData("/css/workflow-nav.css")]
    public async Task Scanner_static_assets_are_served(string path)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task Every_source_list_exports_a_fillable_pdf(string versionId, string sheetId, string date)
    {
        using var client = await ClientWithToken();
        var sheet = ScannerLists.All.Single(v => v.Id == versionId).Sheets.Single(s => s.Id == sheetId);
        var data = new ScannerSubmission
        {
            Date = date, VersionId = versionId, SheetId = sheetId,
            Entries = sheet.Rows.ToDictionary(r => r.Id, _ => new ScannerEntry
                { HandoverTo = "QA Example", ScannerNumber = "SC-123", Comments = "Synthetic test" }),
        };
        var response = await client.PostAsJsonAsync("/api/scanners/download", data);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains($"UPS_scanners_{versionId}_{sheetId}_{date}.pdf", response.Content.Headers.ContentDisposition?.ToString());
        var form = PdfTestHelpers.RequireForm(await response.Content.ReadAsByteArrayAsync());
        foreach (var row in sheet.Rows)
        {
            Assert.Equal("QA Example", PdfTestHelpers.TextFieldValue(form, row.Id + "_handoverTo"));
            Assert.Equal("SC-123", PdfTestHelpers.TextFieldValue(form, row.Id + "_scannerNumber"));
            Assert.Equal("Synthetic test", PdfTestHelpers.TextFieldValue(form, row.Id + "_comments"));
        }
    }

    [Fact]
    public async Task Pdf_matches_excel_title_day_label_and_instruction_line()
    {
        using var client = await ClientWithToken();
        var sheet = ScannerLists.All.Single(v => v.Id == "tuesday-friday").Sheets.Single(s => s.Id == "pd2");
        var data = new ScannerSubmission
        {
            Date = "2026-09-08", VersionId = "tuesday-friday", SheetId = "pd2",
            Entries = sheet.Rows.ToDictionary(r => r.Id, _ => new ScannerEntry()),
        };
        var response = await client.PostAsJsonAsync("/api/scanners/download", data);
        response.EnsureSuccessStatusCode();
        var text = PdfTestHelpers.AllText(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Scanner list pd2", text);
        Assert.Contains("Tuesday / Friday", text);
        Assert.Contains("All two piece with a safety cover", text);
        Assert.Contains("User name", text);
        Assert.DoesNotContain("Everyone must leave", text);
        Assert.DoesNotContain("SUNRISE", text);
    }

    [Fact]
    public void Long_comments_stay_inside_the_visible_field_box()
    {
        var sheet = ScannerLists.All.Single(v => v.Id == "monday").Sheets.Single(s => s.Id == "pd1");
        var data = new ScannerSubmission
        {
            Date = "2026-09-07", VersionId = "monday", SheetId = "pd1",
            Entries = sheet.Rows.ToDictionary(r => r.Id, _ => new ScannerEntry
            {
                HandoverTo = new string('W', 80), ScannerNumber = new string('W', 40), Comments = new string('W', 300),
            }),
        };
        var appearances = PdfTestHelpers.FieldAppearances(new ScannerPdfCreator().Create(data));
        Assert.NotEmpty(appearances);
        foreach (var appearance in appearances)
        {
            Assert.NotEmpty(appearance.DrawnLines);
            foreach (var (baseline, _) in appearance.DrawnLines)
                Assert.InRange(baseline, appearance.ClipBottom, appearance.ClipTop);
        }
        // A comment of 300 characters must be drawn in full, not only stored in the field value.
        Assert.Contains(appearances, appearance => appearance.TotalGlyphs >= 300);
    }

    [Fact]
    public async Task Configured_return_instruction_shows_on_the_page_and_in_the_pdf()
    {
        const string instruction = "QA test wording only: hand the scanner back at the team leader desk.";
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ScannerLists:ReturnInstruction", instruction));
        using var client = configured.CreateClient();
        var html = await client.GetStringAsync("/Scanners");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value));
        Assert.Contains(instruction, html);

        var response = await client.PostAsJsonAsync("/api/scanners/download", new ScannerSubmission
            { Date = "2026-09-07", VersionId = "monday", SheetId = "pd1" });
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var pages = PdfTestHelpers.PageCount(bytes);
        var text = PdfTestHelpers.AllText(bytes);
        Assert.Contains(instruction, text);
        Assert.Equal(pages, Regex.Matches(text, Regex.Escape(instruction)).Count);
    }

    [Theory]
    [InlineData("2026-09-12", "tuesday-friday", "pd1")]
    [InlineData("2026-09-13", "monday", "pd1")]
    [InlineData("2026-09-07", "tuesday-friday", "pd1")]
    [InlineData("2026-09-08", "monday", "pd1")]
    [InlineData("2026-02-30", "monday", "pd1")]
    [InlineData("2026-09-07", "monday", "../../outside")]
    public async Task Invalid_date_version_or_list_is_rejected(string date, string versionId, string sheetId)
    {
        using var client = await ClientWithToken();
        var response = await client.PostAsJsonAsync("/api/scanners/download", new ScannerSubmission
            { Date = date, VersionId = versionId, SheetId = sheetId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scanner_download_requires_antiforgery_but_no_account()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/scanners/download", new ScannerSubmission
            { Date = "2026-09-07", VersionId = "monday", SheetId = "pd1" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("security token", await response.Content.ReadAsStringAsync());
    }
}
