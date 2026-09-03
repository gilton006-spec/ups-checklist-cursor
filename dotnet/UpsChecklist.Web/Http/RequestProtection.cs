using Microsoft.AspNetCore.Antiforgery;

namespace UpsChecklist.Web.Http;

public static class RequestProtection
{
    public static void NoStore(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    public static IResult TextError(string message, int status) =>
        Results.Text(message, "text/plain; charset=utf-8", statusCode: status);

    public static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return TextError("The page security token expired. Copy your entries before refreshing the page.", StatusCodes.Status400BadRequest);
        }
    }
}
