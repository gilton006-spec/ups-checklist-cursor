using System.Text;

namespace UpsChecklist.Web.Http;

public static class BoundedRequestReader
{
    public static async Task<BoundedReadResult> ReadUtf8Async(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > 0 && request.ContentLength > maxBytes)
            return BoundedReadResult.TooLarge();

        await using var payload = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var buffer = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            if (payload.Length + read > maxBytes)
                return BoundedReadResult.TooLarge();
            payload.Write(buffer, 0, read);
        }

        try
        {
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(payload.ToArray());
            return BoundedReadResult.Ok(text);
        }
        catch (DecoderFallbackException)
        {
            return BoundedReadResult.InvalidUtf8();
        }
    }
}

public readonly record struct BoundedReadResult(string? Text, int? StatusCode, string? Error)
{
    public static BoundedReadResult Ok(string text) => new(text, null, null);
    public static BoundedReadResult TooLarge() => new(null, StatusCodes.Status413PayloadTooLarge, "The report is too large. Choose a smaller photo and try again.");
    public static BoundedReadResult InvalidUtf8() => new(null, StatusCodes.Status400BadRequest, "The report must use UTF-8 text.");
    public bool IsSuccess => StatusCode is null && Text is not null;
}
