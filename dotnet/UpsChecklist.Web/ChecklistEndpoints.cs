using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using UpsChecklist.Core;
using UpsChecklist.Core.Reporting;
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
            var data = reports.Parse(payload!);
            var bytes = reports.CreatePdf(data);
            var filename = WhatsAppConstants.ReportFilename(data.PositionId, data.Date);
            return Results.File(bytes, "application/pdf", filename);
        }
        catch (ChecklistValidationException ex)
        {
            return RequestProtection.TextError(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex.Message.Contains("WinAnsi", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("does not support U+", StringComparison.Ordinal))
        {
            return RequestProtection.TextError(
                "Please go back and use standard Latin letters in the text fields, or draw your signature.",
                StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            reports.LogPdfFailure(ex);
            return RequestProtection.TextError(
                "The PDF could not be created. Your checklist is still open. Try again.",
                StatusCodes.Status500InternalServerError);
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
            if (!reports.EmailIsConfigured)
            {
                return Results.Json(new
                {
                    message = "Company email is not configured on this server. Download the PDF or use WhatsApp.",
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var data = reports.Parse(payload!);
            if (!reports.TryAcceptSubmission(payload!))
            {
                return Results.Json(new
                {
                    message = "This same report was just submitted. Check the inbox before sending again. If you already sent it, do not assume a second copy is needed.",
                }, statusCode: StatusCodes.Status409Conflict);
            }

            try
            {
                var bytes = reports.CreatePdf(data);
                var filename = WhatsAppConstants.ReportFilename(data.PositionId, data.Date);
                var outcome = await reports.SendEmailAsync(bytes, filename, context.RequestAborted);
                return Results.Json(new
                {
                    address = outcome.DisplayAddress,
                    message = outcome.UserMessage,
                    devInboxUrl = outcome.DevInboxUrl,
                    usesTempInbox = outcome.UsesTempInbox,
                    deliveryConfirmed = false,
                });
            }
            catch
            {
                reports.ReleaseSubmission(payload!);
                throw;
            }
        }
        catch (ChecklistValidationException ex)
        {
            return RequestProtection.TextError(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            reports.LogEmailFailure(ex);
            return RequestProtection.TextError(
                "The email could not be handed to the mail server. Your checklist is still open. Whether anything arrived is unknown; check before retrying.",
                StatusCodes.Status502BadGateway);
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
