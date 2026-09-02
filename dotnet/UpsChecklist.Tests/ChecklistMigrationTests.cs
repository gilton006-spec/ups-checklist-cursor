using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using UglyToad.PdfPig.AcroForms.Fields;
using UpsChecklist.Core;
using UpsChecklist.Core.Models;

namespace UpsChecklist.Tests;

public class ChecklistMigrationTests : IClassFixture<ChecklistWebApplicationFactory>
{
    private readonly ChecklistWebApplicationFactory _factory;

    public ChecklistMigrationTests(ChecklistWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void All_nine_positions_match_workbook_source()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Data", "workbook-source.json");
        var source = JsonSerializer.Deserialize<WorkbookSource>(
            File.ReadAllText(sourcePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var positions = ChecklistPositions.All;

        Assert.Equal(9, positions.Count);
        Assert.Equal(9, positions.Select(p => p.Id).Distinct().Count());
        Assert.Equal(source.Sheets.Select(s => s.Name), positions.Select(p => p.SourceSheet));
        Assert.Equal([8, 6, 6, 8, 8, 8, 8, 17, 14], positions.Select(p => ChecklistPositions.AllItems(p).Count).ToArray());

        foreach (var position in positions)
        {
            var sheet = source.Sheets.First(s => s.Name == position.SourceSheet);
            var items = ChecklistPositions.AllItems(position);
            Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
            foreach (var item in items)
                Assert.Equal(sheet.Cells[item.SourceCell].Trim(), item.Text);

            var expected = sheet.Images.Where(i => i.Row > 1).Select(i => i.File).ToHashSet();
            var actual = position.Sections.SelectMany(s => s.Images ?? []).Select(i => i.File).ToHashSet();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Monday_rules_warn_without_changing_position()
    {
        var positions = ChecklistPositions.All;
        Assert.NotNull(ChecklistPositions.ScheduleWarning(positions[0], "2026-09-02"));
        Assert.Null(ChecklistPositions.ScheduleWarning(positions[0], "2026-09-07"));
        foreach (var id in new[] { "ps1", "ps2", "pd2" })
            Assert.NotNull(ChecklistPositions.ScheduleWarning(positions.First(p => p.Id == id), "2026-09-07"));
        Assert.Null(ChecklistPositions.ScheduleWarning(positions.First(p => p.Id == "pd1"), "2026-09-07"));
    }

    [Fact]
    public async Task Home_page_lists_all_nine_positions_without_preselection()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");
        foreach (var position in ChecklistPositions.All)
            Assert.Contains(position.Label, html);
        Assert.Contains("Choose your position", html);
        Assert.Contains("Demo only. Not UPS approved.", html);
        Assert.DoesNotContain("Open the right checklist with one tap", html);
        var marker = html.IndexOf("positions-data", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        var jsonStart = html.IndexOf('>', marker) + 1;
        var jsonEnd = html.IndexOf("</script>", jsonStart, StringComparison.Ordinal);
        var json = html[jsonStart..jsonEnd].Replace(" ", "");
        foreach (var position in ChecklistPositions.All)
            Assert.Contains($"\"id\":\"{position.Id}\"", json);
    }

    [Theory]
    [InlineData("ps1-ps2-monday", "2026-09-07")]
    [InlineData("ps1", "2026-09-02")]
    [InlineData("ps2", "2026-09-02")]
    [InlineData("pd1", "2026-09-02")]
    [InlineData("pd2", "2026-09-07")]
    [InlineData("pd3", "2026-09-02")]
    [InlineData("pd4", "2026-09-02")]
    [InlineData("smalls", "2026-09-02")]
    [InlineData("matrix", "2026-09-02")]
    public async Task Download_endpoint_generates_editable_pdf(string positionId, string date)
    {
        var position = ChecklistPositions.Get(positionId)!;
        var data = SampleData(position, date);
        var bytes = await DownloadPdfAsync(data);

        var form = PdfTestHelpers.RequireForm(bytes);
        Assert.Equal(data.Name, PdfTestHelpers.TextFieldValue(form, "name"));
        Assert.Equal(data.Date, PdfTestHelpers.TextFieldValue(form, "date"));
        Assert.Equal("7", PdfTestHelpers.TextFieldValue(form, "package_count"));
        Assert.Equal(data.Signature, PdfTestHelpers.TextFieldValue(form, "signature"));

        foreach (var item in ChecklistPositions.AllItems(position))
            Assert.Equal(data.Checks[item.Id], PdfTestHelpers.CheckboxChecked(form, item.Id));

        var expectedFields = ChecklistPositions.AllItems(position).Count + 5
            + position.Sections.Count(s => s.RemarksKey is not null);
        Assert.Equal(expectedFields, form.Fields.Count);
        Assert.Contains(position.Label, PdfTestHelpers.Title(bytes));
    }

    [Fact]
    public async Task Long_remarks_continue_without_truncation()
    {
        var position = ChecklistPositions.Get("matrix")!;
        var remarks = string.Concat(Enumerable.Repeat("Follow-up required. ", 100));
        if (remarks.Length > 2000)
            remarks = remarks[..2000];
        var recirculation = string.Concat(Enumerable.Repeat("Area issue noted. ", 55));
        if (recirculation.Length > 1000)
            recirculation = recirculation[..1000];
        var data = SampleData(position, "2026-09-02", remarks: remarks,
            sectionRemarks: new Dictionary<string, string> { ["recirculation"] = recirculation });

        var bytes = await DownloadPdfAsync(data);
        var form = PdfTestHelpers.RequireForm(bytes);
        var parts = form.Fields
            .OfType<AcroTextField>()
            .Where(f => f.Information.PartialName is not null
                && System.Text.RegularExpressions.Regex.IsMatch(f.Information.PartialName, @"^remarks(?:_continued_\d+)?$"))
            .Select(f => f.Value ?? "")
            .ToList();
        Assert.Equal(
            Regex.Replace(data.Remarks, @"\s", ""),
            Regex.Replace(string.Concat(parts), @"\s", ""));
    }

    [Fact]
    public async Task Drawn_signatures_embed_without_signature_form_widget()
    {
        var position = ChecklistPositions.Get("pd4")!;
        var data = SampleData(position, "2026-09-02", signature: "", drawn: PngDataUrl("image13.png"));

        var bytes = await DownloadPdfAsync(data);
        var form = PdfTestHelpers.RequireForm(bytes);
        Assert.False(PdfTestHelpers.HasField(form, "signature"));
        Assert.Equal(data.Name, PdfTestHelpers.TextFieldValue(form, "name"));
    }

    [Fact]
    public async Task Portrait_evidence_is_embedded_with_editable_form_fields_intact()
    {
        var position = ChecklistPositions.Get("pd4")!;
        var data = SampleData(position, "2026-09-02", evidencePhoto: PngDataUrl("image13.png"));

        var bytes = await DownloadPdfAsync(data);
        Assert.True(PdfTestHelpers.HasAnyImage(bytes));
        Assert.Equal(2, PdfTestHelpers.PageCount(bytes));
        Assert.Equal(data.Signature, PdfTestHelpers.TextFieldValue(PdfTestHelpers.RequireForm(bytes), "signature"));
    }

    [Fact]
    public async Task All_nine_positions_embed_jpeg_camera_evidence()
    {
        foreach (var position in ChecklistPositions.All)
        {
            var data = SampleData(position, position.Schedule == "monday" ? "2026-09-07" : "2026-09-02", evidencePhoto: EvidenceFixtures.JpegPhoto);
            var bytes = await DownloadPdfAsync(data);
            Assert.True(PdfTestHelpers.HasAnyImage(bytes), position.Id);
        }
    }

    [Fact]
    public async Task Unsupported_malformed_and_oversized_evidence_is_rejected()
    {
        var data = SampleData(ChecklistPositions.All[0], "2026-09-07");
        foreach (var evidence in new[]
                 {
                     "data:image/svg+xml;base64,AAAA",
                     "data:image/jpeg;base64,AAAA",
                     "data:image/png;base64," + new string('A', 800_001),
                 })
        {
            var submission = SampleData(ChecklistPositions.All[0], "2026-09-07", evidencePhoto: evidence);
            var response = await PostDownloadAsync(submission);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var client = _factory.CreateClient();
        using var content = new StringContent(new string('x', 1_600_001), null, "application/x-www-form-urlencoded");
        var large = await client.PostAsync("/api/download", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
    }

    [Fact]
    public async Task Invalid_submissions_are_rejected()
    {
        var invalid = new List<ChecklistSubmission>
        {
            SampleData(ChecklistPositions.All[0], "2026-09-07", positionId: "unknown"),
            SampleData(ChecklistPositions.All[0], "2026-09-07", checks: new Dictionary<string, bool> { ["unrelated"] = true }),
            SampleData(ChecklistPositions.All[0], "2026-02-30"),
            SampleData(ChecklistPositions.All[0], "2026-09-07", remarks: new string('x', 2001)),
            SampleData(ChecklistPositions.All[0], "2026-09-07", sectionRemarks: new Dictionary<string, string> { ["unrelated"] = "test" }),
        };

        foreach (var submission in invalid)
        {
            var response = await PostDownloadAsync(submission);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public void Evidence_validation_rejects_oversized_and_malformed_photos()
    {
        Assert.True(EvidenceValidation.ValidEvidencePhoto(""));
        Assert.True(EvidenceValidation.ValidEvidencePhoto(EvidenceFixtures.JpegPhoto));
        Assert.True(EvidenceValidation.ValidEvidencePhoto(PngDataUrl("image13.png")));

        var png = File.ReadAllBytes(AssetPath("image13.png"));
        var huge = (byte[])png.Clone();
        huge[16] = 0x00;
        huge[17] = 0x01;
        huge[18] = 0x86;
        huge[19] = 0xA0;
        Assert.False(EvidenceValidation.ValidEvidencePhoto("data:image/png;base64," + Convert.ToBase64String(huge)));

        foreach (var bad in new[] { "data:image/jpeg;base64,AAAA", "data:image/png;base64,AAAA", "data:text/html;base64,AAAA" })
            Assert.False(EvidenceValidation.ValidEvidencePhoto(bad));
    }

    [Fact]
    public void WhatsApp_chat_url_targets_configured_number_without_report_data()
    {
        var url = new Uri(WhatsAppConstants.HandoverChatUrl);
        Assert.Equal("https", url.Scheme);
        Assert.Equal("/31626149058", url.AbsolutePath);
        Assert.Equal("+31 626149058", WhatsAppConstants.HandoverNumber);
        Assert.Equal("Sunrise checklist. I will attach the report PDF here.", QueryHelpers.ParseQuery(url.Query)["text"].ToString());
        Assert.Equal("UPS_pd4_2026-09-02.pdf", WhatsAppConstants.ReportFilename("pd4", "2026-09-02"));
    }

    [Fact]
    public void Email_handover_targets_configured_address()
    {
        Assert.Equal("gilton93@hotmail.com", EmailConstants.HandoverAddress);
        Assert.Contains("gilton93@hotmail.com", EmailConstants.MailtoUrl("UPS_pd4_2026-09-02.pdf"));
    }

    [Fact]
    public async Task Email_handover_without_smtp_returns_service_unavailable()
    {
        var client = _factory.CreateClient();
        var data = SampleData(ChecklistPositions.All[0], "2026-09-07");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["checklist"] = JsonSerializer.Serialize(data),
        });
        var response = await client.PostAsync("/api/email-handover", content);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<EmailHandoverErrorResponse>();
        Assert.NotNull(json);
        Assert.Contains("not configured", json!.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record EmailHandoverErrorResponse(string Message);

    [Fact]
    public async Task Handover_config_lists_email_and_whatsapp()
    {
        var client = _factory.CreateClient();
        var json = await client.GetFromJsonAsync<HandoverConfigResponse>("/api/handover-config");
        Assert.NotNull(json);
        Assert.Equal("gilton93@hotmail.com", json!.EmailAddress);
        Assert.Equal("+31 626149058", json.WhatsappNumber);
        Assert.False(json.EmailConfigured);
    }

    private sealed record HandoverConfigResponse(string EmailAddress, bool EmailConfigured, string WhatsappNumber);

    private async Task<byte[]> DownloadPdfAsync(ChecklistSubmission data)
    {
        var response = await PostDownloadAsync(data);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Contains(data.PositionId, response.Content.Headers.ContentDisposition?.FileName ?? "");
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<HttpResponseMessage> PostDownloadAsync(ChecklistSubmission data)
    {
        var client = _factory.CreateClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["checklist"] = JsonSerializer.Serialize(data),
        });
        return await client.PostAsync("/api/download", content);
    }

    private static ChecklistSubmission SampleData(
        Position position,
        string date,
        string? positionId = null,
        Dictionary<string, bool>? checks = null,
        string? remarks = null,
        Dictionary<string, string>? sectionRemarks = null,
        string? evidencePhoto = null,
        string? drawn = null,
        string? signature = null) => new()
    {
        PositionId = positionId ?? position.Id,
        Date = date,
        Name = "DEMO José Example",
        Checks = checks ?? ChecklistPositions.AllItems(position)
            .Select((item, index) => (item.Id, Value: index % 2 == 0))
            .ToDictionary(x => x.Id, x => x.Value),
        Count = "7",
        Remarks = remarks ?? "DEMO: One check needs follow-up. Team leader review required.",
        SectionRemarks = sectionRemarks ?? position.Sections
            .Where(s => s.RemarksKey is not null)
            .ToDictionary(s => s.RemarksKey!, _ => "DEMO: Area reviewed; follow-up recorded."),
        EvidencePhoto = evidencePhoto ?? "",
        Drawn = drawn ?? "",
        Signature = signature ?? "José Example",
    };

    private static string PngDataUrl(string file) =>
        "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(AssetPath(file)));

    private static string AssetPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Workbook", file);
}
