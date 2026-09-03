using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using UpsChecklist.Core;
using UpsChecklist.Web.Http;
using UpsChecklist.Web.Services;

namespace UpsChecklist.Web;

public static class ChecklistEndpoints
{
    public static void MapChecklistEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "ok" }))
            .AllowAnonymous()
            .WithName("health");

        app.MapGet("/api/handover-config", (HandoverRuntimeInfo runtime, Microsoft.Extensions.Options.IOptions<HandoverEmailOptions> options) =>
            Results.Json(new
            {
                emailAddress = runtime.DisplayAddress,
                emailConfigured = options.Value.IsConfigured,
                whatsappNumber = WhatsAppConstants.HandoverNumber,
                usesTempInbox = runtime.UsesTempInbox,
                devInboxUrl = runtime.DevInboxUrl,
            }));

        app.MapPost("/api/download", DownloadAsync)
            .RequireRateLimiting("reports");
        app.MapPost("/api/email-handover", EmailAsync)
            .RequireRateLimiting("reports");
    }

    internal static async Task<IResult> DownloadAsync(
        HttpContext context,
        ChecklistReportService reports,
        IAntiforgery antiforgery)
    {
        RequestProtection.NoStore(context.Response);
        var (payload, error) = await ReadChecklistAsync(context, antiforgery);
        if (error is not null) return error;

        try
        {
            return reports.CreatePdfReport(payload!) switch
            {
                PdfCreateResult.Ok ok => Results.File(ok.Report.Bytes, "application/pdf", ok.Report.Filename),
                PdfCreateResult.Invalid invalid => RequestProtection.TextError(invalid.Message, StatusCodes.Status400BadRequest),
                PdfCreateResult.FontRejected font => RequestProtection.TextError(font.Message, StatusCodes.Status400BadRequest),
                PdfCreateResult.Failed failed => RequestProtection.TextError(failed.Message, StatusCodes.Status500InternalServerError),
                _ => RequestProtection.TextError("Unexpected PDF result.", StatusCodes.Status500InternalServerError),
            };
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
    }

    internal static async Task<IResult> EmailAsync(
        HttpContext context,
        ChecklistReportService reports,
        IAntiforgery antiforgery)
    {
        RequestProtection.NoStore(context.Response);
        var (payload, error) = await ReadChecklistAsync(context, antiforgery);
        if (error is not null) return error;

        try
        {
            return await reports.SendEmailHandoverAsync(payload!, context.RequestAborted) switch
            {
                EmailHandoverResult.NotConfigured => Results.Json(new
                {
                    message = "Company email is not configured on this server. Download the PDF or use WhatsApp.",
                }, statusCode: StatusCodes.Status503ServiceUnavailable),
                EmailHandoverResult.Duplicate => Results.Json(new
                {
                    message = "This same report was just submitted. Check the inbox before sending again. If you already sent it, do not assume a second copy is needed.",
                }, statusCode: StatusCodes.Status409Conflict),
                EmailHandoverResult.Accepted accepted => Results.Json(new
                {
                    address = accepted.Outcome.DisplayAddress,
                    message = accepted.Outcome.UserMessage,
                    devInboxUrl = accepted.Outcome.DevInboxUrl,
                    usesTempInbox = accepted.Outcome.UsesTempInbox,
                    deliveryConfirmed = false,
                }),
                EmailHandoverResult.Invalid invalid => RequestProtection.TextError(invalid.Message, StatusCodes.Status400BadRequest),
                EmailHandoverResult.FontRejected font => RequestProtection.TextError(font.Message, StatusCodes.Status400BadRequest),
                EmailHandoverResult.FailedBeforeSend failed => RequestProtection.TextError(failed.Message, StatusCodes.Status500InternalServerError),
                EmailHandoverResult.OutcomeUnknown unknown => RequestProtection.TextError(unknown.Message, StatusCodes.Status502BadGateway),
                _ => RequestProtection.TextError("Unexpected email result.", StatusCodes.Status500InternalServerError),
            };
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
    }

    private static async Task<(string? Payload, IResult? Error)> ReadChecklistAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.ContentLength is > ChecklistValidator.MaxBodyBytes)
            return (null, RequestProtection.TextError("The report is too large. Choose a smaller photo and try again.", StatusCodes.Status413PayloadTooLarge));

        var antiforgeryError = await RequestProtection.ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null)
            return (null, antiforgeryError);

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var payload = form["checklist"].ToString();
            return string.IsNullOrWhiteSpace(payload)
                ? (null, RequestProtection.TextError("The checklist is missing.", StatusCodes.Status400BadRequest))
                : (payload, null);
        }

        var contentType = context.Request.ContentType ?? "";
        if (!contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return (null, RequestProtection.TextError("Send the checklist as form data or JSON.", StatusCodes.Status415UnsupportedMediaType));
        }

        var read = await BoundedRequestReader.ReadUtf8Async(context.Request, ChecklistValidator.MaxBodyBytes, context.RequestAborted);
        if (!read.IsSuccess)
            return (null, RequestProtection.TextError(read.Error!, read.StatusCode!.Value));

        var extracted = ChecklistPayloadReader.ExtractFromBody(read.Text!, contentType);
        return string.IsNullOrWhiteSpace(extracted)
            ? (null, RequestProtection.TextError("The checklist is missing.", StatusCodes.Status400BadRequest))
            : (extracted, null);
    }
}
