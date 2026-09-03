using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using UpsChecklist.Core.Pdf;
using UpsChecklist.Core.Reporting;
using UpsChecklist.Core.Scanners;
using UpsChecklist.Web.Http;

namespace UpsChecklist.Web;

public static class ScannerEndpoints
{
    public static void MapScannerEndpoints(this WebApplication app) =>
        app.MapPost("/api/scanners/download", DownloadAsync)
            .RequireRateLimiting("reports");

    internal static async Task<IResult> DownloadAsync(HttpContext context, IScannerPdfCreator creator,
        ScannerListOptions options, IAntiforgery antiforgery)
    {
        RequestProtection.NoStore(context.Response);
        try
        {
            if (!context.Request.HasJsonContentType())
                return RequestProtection.TextError("Send the scanner report as JSON.", StatusCodes.Status415UnsupportedMediaType);
            var antiforgeryError = await RequestProtection.ValidateAntiforgeryAsync(context, antiforgery);
            if (antiforgeryError is not null)
                return antiforgeryError;

            var read = await BoundedRequestReader.ReadUtf8Async(context.Request, ScannerValidator.MaxBodyBytes, context.RequestAborted);
            if (!read.IsSuccess)
            {
                var message = read.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "The scanner report is too large."
                    : read.StatusCode == StatusCodes.Status400BadRequest && read.Error!.Contains("UTF-8", StringComparison.Ordinal)
                        ? "The scanner report must use UTF-8 text."
                        : read.Error!;
                return RequestProtection.TextError(message, read.StatusCode!.Value);
            }

            var data = ScannerValidator.ParseAndValidate(read.Text!);
            var bytes = creator.Create(data, options.ReturnInstruction);
            return Results.File(bytes, "application/pdf", $"UPS_scanners_{data.VersionId}_{data.SheetId}_{data.Date}.pdf");
        }
        catch (ScannerValidationException ex)
        {
            return RequestProtection.TextError(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not support U+", StringComparison.Ordinal))
        {
            return RequestProtection.TextError("A character is not supported by the PDF font. Use standard letters and numbers.", StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ScannerPdf")
                .LogError(ex, "Scanner PDF generation failed.");
            return RequestProtection.TextError("The PDF could not be created. Please try again.", StatusCodes.Status500InternalServerError);
        }
    }
}
