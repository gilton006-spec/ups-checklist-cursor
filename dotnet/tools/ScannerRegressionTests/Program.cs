using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using UpsChecklist.Core.Pdf;
using UpsChecklist.Core.Scanners;
using UpsChecklist.Web;

if (args.Length != 1) throw new ArgumentException("Supply the directory for synthetic PDF test outputs.");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var checks = 0;
void Assert(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
void Reject(string json)
{
    try { ScannerValidator.ParseAndValidate(json); }
    catch (ScannerValidationException) { checks++; return; }
    throw new Exception("Invalid payload was accepted: " + json);
}

Assert(ScannerLists.All.SelectMany(v => v.Sheets).Sum(s => s.Rows.Count) == 80, "All 80 source rows must be present.");
Assert(ScannerLists.All[0].Sheets.Select(s => s.Rows.Count).SequenceEqual(new[] { 3, 18, 3, 6, 8 }), "Monday row counts.");
Assert(ScannerLists.All[1].Sheets.Select(s => s.Rows.Count).SequenceEqual(new[] { 3, 14, 14, 6, 5 }), "Tuesday-Friday row counts.");
Assert(ScannerLists.All[0].Sheets.Single(s => s.Id == "pd2").Title == "Scanner list pd1 2 of 2", "Preserve Monday's PD1 second page.");
for (var offset = 0; offset < 7; offset++)
{
    var date = new DateOnly(2026, 9, 7).AddDays(offset);
    Assert(ScannerLists.ForDate(date)?.Id == (offset == 0 ? "monday" : offset < 5 ? "tuesday-friday" : null), "Weekday selection.");
}
foreach (var json in new[] { "null", "{}", "{", "[]", "{\"date\":\"2026-02-30\"}",
    "{\"date\":\"2026-09-12\"}", "{\"date\":\"2026-09-07\",\"versionId\":\"tuesday-friday\"}",
    "{\"date\":\"2026-09-07\",\"versionId\":\"monday\",\"sheetId\":\"bad\"}" }) Reject(json);
var valid = new ScannerSubmission { Date = "2026-09-07", VersionId = "monday", SheetId = "pd1" };
var baseJson = JsonSerializer.Serialize(valid, new JsonSerializerOptions(JsonSerializerDefaults.Web));
Reject(baseJson.Replace("\"entries\":{}", "\"entries\":null"));
Reject(baseJson.Replace("\"entries\":{}", "\"entries\":{\"r999\":{}}"));
Reject(baseJson.Replace("\"entries\":{}", "\"entries\":{\"r5\":null}"));
Reject(baseJson.Replace("\"entries\":{}", "\"entries\":{\"r5\":{\"handoverTo\":null}}"));
Reject(baseJson.Replace("\"entries\":{}", "\"entries\":{\"r5\":{\"position\":\"wrong\"}}"));
Reject(baseJson.Replace("\"entries\":{}", "\"entries\":{\"r5\":{\"scannerNumber\":\"bad\\nline\"}}"));
valid.Entries["r5"] = new() { Comments = new string('a', 301) };
Reject(JsonSerializer.Serialize(valid));
valid.Entries.Clear();

var manifest = new List<object>();
foreach (var culture in new[] { "en-US", "nl-NL" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
    foreach (var version in ScannerLists.All)
    foreach (var sheet in version.Sheets)
    foreach (var blank in new[] { true, false })
    {
        var data = new ScannerSubmission
        {
            Date = version.Id == "monday" ? "2026-09-07" : "2026-09-08", VersionId = version.Id, SheetId = sheet.Id,
            Entries = blank ? [] : sheet.Rows.ToDictionary(r => r.Id, r => new ScannerEntry
            {
                HandoverTo = "Zoë Müller - QA", ScannerNumber = "SC-" + r.SourceRow,
                Comments = "Synthetic test only. Checked for handover.", Position = r.Position.Length == 0 ? "QA zone" : "",
            }),
        };
        var file = $"{version.Id}-{sheet.Id}-{culture}-{(blank ? "blank" : "filled")}.pdf";
        var bytes = new ScannerPdfCreator().Create(data);
        Assert(bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8), "PDF header.");
        File.WriteAllBytes(Path.Combine(output, file), bytes);
        manifest.Add(new { file, data, sheet });
    }
}
valid.Entries["r5"] = new() { HandoverTo = new string('W', 80), ScannerNumber = new string('W', 40), Comments = new string('W', 300) };
File.WriteAllBytes(Path.Combine(output, "long-fields.pdf"), new ScannerPdfCreator().Create(valid,
    "QA instruction only: return location must be confirmed before production."));
manifest.Add(new { file = "long-fields.pdf", data = valid, sheet = ScannerLists.All[0].Sheets.Single(s => s.Id == "pd1") });
File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

// Exercise the real endpoint and real anti-forgery service without a listening server.
var services = new ServiceCollection();
services.AddLogging();
services.AddDataProtection().UseEphemeralDataProtectionProvider();
services.AddAntiforgery();
// This standalone console test owns one provider; there is no application provider.
#pragma warning disable ASP0000
await using var provider = services.BuildServiceProvider();
#pragma warning restore ASP0000
var antiforgery = provider.GetRequiredService<IAntiforgery>();
var tokenContext = new DefaultHttpContext { RequestServices = provider };
var tokens = antiforgery.GetAndStoreTokens(tokenContext);
var cookie = tokenContext.Response.Headers.SetCookie.ToString().Split(';')[0];
async Task<IResult> Request(string json, bool token = true, string contentType = "application/json", bool length = true)
{
    var ctx = new DefaultHttpContext { RequestServices = provider };
    ctx.Request.Method = "POST";
    ctx.Request.ContentType = contentType;
    ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
    if (length) ctx.Request.ContentLength = ctx.Request.Body.Length;
    if (token)
    {
        ctx.Request.Headers.Cookie = cookie;
        ctx.Request.Headers["RequestVerificationToken"] = tokens.RequestToken;
    }
    var result = await ScannerEndpoints.DownloadAsync(ctx, new ScannerPdfCreator(), new(), antiforgery);
    Assert(ctx.Response.Headers.CacheControl == "no-store", "No-store header.");
    Assert(ctx.Response.Headers["X-Content-Type-Options"] == "nosniff", "Nosniff header.");
    return result;
}
Assert(await Request(baseJson) is FileContentHttpResult { ContentType: "application/pdf" }, "Valid download.");
Assert(await Request(baseJson, token: false) is ContentHttpResult { StatusCode: 400 }, "CSRF token required.");
Assert(await Request(baseJson, contentType: "text/plain") is ContentHttpResult { StatusCode: 415 }, "JSON content type required.");
Assert(await Request(new string('x', ScannerValidator.MaxBodyBytes + 1)) is ContentHttpResult { StatusCode: 413 }, "Known payload size limit.");
Assert(await Request(new string('x', ScannerValidator.MaxBodyBytes + 1), length: false) is ContentHttpResult { StatusCode: 413 }, "Chunked payload size limit.");
Assert(await Request("null") is ContentHttpResult { StatusCode: 400 }, "Invalid JSON body rejected.");
Assert(await Request(baseJson.Replace("2026-09-07", "2026-09-12")) is ContentHttpResult { StatusCode: 400 }, "Weekend rejected by endpoint.");
Console.WriteLine($"PASS: {checks} checks, {manifest.Count} scanner PDF fixtures. No SMTP, WhatsApp or listening web server used.");
