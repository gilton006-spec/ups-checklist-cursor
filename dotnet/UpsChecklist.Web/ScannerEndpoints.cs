using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using UpsChecklist.Core.Pdf;
using UpsChecklist.Core.Scanners;

namespace UpsChecklist.Web;

public static class ScannerEndpoints
{
    public static void MapScannerEndpoints(this WebApplication app) =>
        app.MapPost("/api/scanners/download", DownloadAsync);

    internal static async Task<IResult> DownloadAsync(HttpContext context, ScannerPdfCreator creator,
        ScannerListOptions options, IAntiforgery antiforgery)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            if (!context.Request.HasJsonContentType())
                return Error("Send the scanner report as JSON.", 415);
            if (context.Request.ContentLength > ScannerValidator.MaxBodyBytes)
                return Error("The scanner report is too large.", 413);
            await antiforgery.ValidateRequestAsync(context);
            using var payload = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            // Count bytes even for chunked requests with no Content-Length header.
            while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (payload.Length + read > ScannerValidator.MaxBodyBytes)
                    return Error("The scanner report is too large.", 413);
                payload.Write(buffer, 0, read);
            }
            var json = new UTF8Encoding(false, true).GetString(payload.ToArray());
            var data = ScannerValidator.ParseAndValidate(json);
            var bytes = creator.Create(data, options.ReturnInstruction);
            return Results.File(bytes, "application/pdf", $"UPS_scanners_{data.VersionId}_{data.SheetId}_{data.Date}.pdf");
        }
        catch (AntiforgeryValidationException)
        {
            return Error("The page security token expired. Copy your entries before refreshing the page.", 400);
        }
        catch (ScannerValidationException ex) { return Error(ex.Message, 400); }
        catch (DecoderFallbackException) { return Error("The scanner report must use UTF-8 text.", 400); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not support U+", StringComparison.Ordinal))
        {
            return Error("A character is not supported by the PDF font. Use standard letters and numbers.", 400);
        }
        catch (Exception ex)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ScannerPdf")
                .LogError(ex, "Scanner PDF generation failed.");
            return Error("The PDF could not be created. Please try again.", 500);
        }
    }

    private static IResult Error(string text, int status) =>
        Results.Text(text, "text/plain; charset=utf-8", statusCode: status);
}
