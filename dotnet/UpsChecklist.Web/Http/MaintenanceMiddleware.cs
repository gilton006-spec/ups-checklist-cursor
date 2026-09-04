namespace UpsChecklist.Web.Http;

/// <summary>
/// Emergency maintenance gate. When active, every request except /health returns 503
/// with Cache-Control: no-store. Does not delete drafts or reports (none are stored server-side).
/// Enable with APP_MAINTENANCE_MODE=true (durable production switch), or by creating
/// the file named in APP_MAINTENANCE_FLAG_FILE (local/operator only; not an HTTP API;
/// not durable alone on Fly's ephemeral filesystem).
/// </summary>
public sealed class MaintenanceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsMaintenanceActive())
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        var accept = context.Request.Headers.Accept.ToString();
        var wantsHtml = context.Request.Method == HttpMethods.Get
            && (string.IsNullOrEmpty(accept)
                || accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            && !context.Request.Path.StartsWithSegments("/api")
            && !LooksLikeStaticAsset(context.Request.Path);

        if (!wantsHtml)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "This checklist app is temporarily offline for maintenance. Your browser drafts are not deleted. Try again when the shift tool is back.");
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Maintenance — UPS Sunrise Checklist</title>
              <style>
                body { margin: 0; font-family: "Segoe UI", system-ui, sans-serif; background: #f3f0ea; color: #412511; }
                main { max-width: 32rem; margin: 0 auto; padding: 2rem 1.25rem 3rem; }
                h1 { font-size: 1.75rem; line-height: 1.25; margin: 0 0 0.75rem; }
                p { font-size: 1.05rem; line-height: 1.5; margin: 0 0 1rem; }
              </style>
            </head>
            <body>
              <main>
                <h1>Temporarily offline</h1>
                <p>The Sunrise checklist is in emergency maintenance. You cannot submit reports right now.</p>
                <p>Entries saved in this browser tab are not deleted on the server — nothing is archived here. Come back when your team leader says the tool is open again.</p>
              </main>
            </body>
            </html>
            """);
    }

    private static bool LooksLikeStaticAsset(PathString path)
    {
        var value = path.Value ?? "";
        return value.StartsWith("/workbook", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsMaintenanceActive()
    {
        var mode = Environment.GetEnvironmentVariable("APP_MAINTENANCE_MODE");
        if (string.Equals(mode, "true", StringComparison.OrdinalIgnoreCase) || mode == "1")
            return true;

        var flagFile = Environment.GetEnvironmentVariable("APP_MAINTENANCE_FLAG_FILE");
        return !string.IsNullOrWhiteSpace(flagFile) && File.Exists(flagFile);
    }
}
