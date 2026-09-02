using System.Text;
using Microsoft.Extensions.Options;
using UpsChecklist.Core;
using UpsChecklist.Core.Pdf;

var builder = WebApplication.CreateBuilder(args);
var localSettingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.Development.local.json");
if (builder.Environment.IsDevelopment() && File.Exists(localSettingsPath))
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);

var emailOptions = builder.Configuration
    .GetSection(HandoverEmailOptions.SectionName)
    .Get<HandoverEmailOptions>() ?? new HandoverEmailOptions();
var runtimeInfo = HandoverRuntimeInfo.ForOptions(emailOptions);

if (builder.Environment.IsDevelopment() && !emailOptions.IsConfigured)
{
    var temp = await EtherealEmailProvisioner.ProvisionAsync();
    emailOptions = temp.Options;
    runtimeInfo = new HandoverRuntimeInfo
    {
        DisplayAddress = temp.InboxAddress,
        DevInboxUrl = temp.InboxUrl,
        UsesTempInbox = true,
    };
    Console.WriteLine("Development test inbox ready.");
    Console.WriteLine($"Send report delivers to: {temp.InboxAddress}");
    Console.WriteLine($"View at: {temp.InboxUrl}");
    Console.WriteLine($"Login: {temp.Username} / {temp.Password}");
}

builder.Services.AddRazorPages();
builder.Services.AddSingleton<ChecklistPdfCreator>();
builder.Services.AddSingleton<HandoverEmailSender>();
builder.Services.AddSingleton(emailOptions);
builder.Services.AddSingleton(runtimeInfo);
builder.Services.AddSingleton<IOptions<HandoverEmailOptions>>(_ => Options.Create(emailOptions));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.MapGet("/api/handover-config", (HandoverEmailOptions options, HandoverRuntimeInfo runtime) =>
    Results.Json(new
    {
        emailAddress = runtime.DisplayAddress,
        emailConfigured = options.IsConfigured,
        whatsappNumber = WhatsAppConstants.HandoverNumber,
        usesTempInbox = runtime.UsesTempInbox,
        devInboxUrl = runtime.DevInboxUrl,
    }));

app.MapPost("/api/download", async (HttpContext context, ChecklistPdfCreator creator) =>
    await CreatePdfResponseAsync(context, creator));

app.MapPost("/api/email-handover", async (
    HttpContext context,
    ChecklistPdfCreator creator,
    HandoverEmailSender sender,
    HandoverEmailOptions options,
    HandoverRuntimeInfo runtime) =>
{
    try
    {
        var (payload, rejectedStatus, rejectedMessage) = await ReadChecklistPayloadAsync(context);
        if (rejectedStatus is not null)
            return Results.Content(rejectedMessage!, "text/plain; charset=utf-8", statusCode: rejectedStatus.Value);

        var data = ChecklistValidator.ParseAndValidate(payload!);
        var bytes = creator.Create(data);
        var filename = WhatsAppConstants.ReportFilename(data.PositionId, data.Date);

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (!options.IsConfigured)
        {
            return Results.Json(new
            {
                message = "Company email is not configured on this server.",
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await sender.SendAsync(bytes, filename, options, context.RequestAborted);
        return Results.Json(new
        {
            address = runtime.DisplayAddress,
            message = runtime.UsesTempInbox
                ? $"Report sent to {runtime.DisplayAddress}. Log in at Ethereal to view it."
                : "Report sent to the company inbox. Delivery is not confirmed.",
            devInboxUrl = runtime.DevInboxUrl,
            usesTempInbox = runtime.UsesTempInbox,
        });
    }
    catch (ChecklistValidationException ex)
    {
        return Results.Content(ex.Message, "text/plain; charset=utf-8", statusCode: 400);
    }
    catch
    {
        return Results.Content(
            "The email could not be sent. Your checklist is still open. Try again.",
            "text/plain; charset=utf-8",
            statusCode: 400);
    }
});

app.Run();

static async Task<IResult> CreatePdfResponseAsync(HttpContext context, ChecklistPdfCreator creator)
{
    try
    {
        var (payload, rejectedStatus, rejectedMessage) = await ReadChecklistPayloadAsync(context);
        if (rejectedStatus is not null)
            return Results.Content(rejectedMessage!, "text/plain; charset=utf-8", statusCode: rejectedStatus.Value);

        var data = ChecklistValidator.ParseAndValidate(payload!);
        var bytes = creator.Create(data);
        var filename = WhatsAppConstants.ReportFilename(data.PositionId, data.Date);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.File(bytes, "application/pdf", filename);
    }
    catch (ChecklistValidationException ex)
    {
        return Results.Content(ex.Message, "text/plain; charset=utf-8", statusCode: 400);
    }
    catch (Exception ex) when (ex.Message.Contains("WinAnsi", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content(
            "Please go back and use standard Latin letters in the text fields, or draw your signature.",
            "text/plain; charset=utf-8",
            statusCode: 400);
    }
    catch
    {
        return Results.Content(
            "The PDF could not be created. Your checklist is still open in the original tab. Please return to it and try again.",
            "text/plain; charset=utf-8",
            statusCode: 400);
    }
}

static async Task<(string? Payload, int? RejectedStatusCode, string? RejectedMessage)> ReadChecklistPayloadAsync(HttpContext context)
{
    if (context.Request.ContentLength is > ChecklistValidator.MaxBodyBytes)
        return (null, 413, "The report is too large. Choose a smaller photo and try again.");

    if (context.Request.HasFormContentType)
    {
        var form = await context.Request.ReadFormAsync();
        var payload = form["checklist"].ToString();
        return string.IsNullOrWhiteSpace(payload)
            ? (null, 400, "The checklist is missing.")
            : (payload, null, null);
    }

    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    if (body.Length > ChecklistValidator.MaxBodyBytes)
        return (null, 413, "The report is too large. Choose a smaller photo and try again.");

    var checklistRaw = ChecklistPayloadReader.ExtractFromBody(body, context.Request.ContentType);
    return string.IsNullOrWhiteSpace(checklistRaw)
        ? (null, 400, "The checklist is missing.")
        : (checklistRaw, null, null);
}

public partial class Program;
