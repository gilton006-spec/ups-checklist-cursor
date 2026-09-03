using Microsoft.Extensions.Options;
using UpsChecklist.Core;

namespace UpsChecklist.Web.Http;

public sealed class SiteAccessMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AnonymousPaths =
    [
        "/Login",
        "/Logout",
        "/Error",
        "/health",
    ];

    public async Task InvokeAsync(HttpContext context, IOptions<SiteAccessOptions> accessOptions, IHostEnvironment environment)
    {
        var access = accessOptions.Value;
        var productionLike = environment.IsProduction()
            || environment.IsStaging()
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLY_APP_NAME"));

        if (access.IsMisconfigured(productionLike))
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/health"))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "Site access is not configured. Set SiteAccess__Password before serving this host.");
            return;
        }

        if (!access.IsEnabled)
        {
            await next(context);
            return;
        }

        var requestPath = context.Request.Path;
        if (AnonymousPaths.Any(p => requestPath.StartsWithSegments(p)))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        if (requestPath.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Sign in required.");
            return;
        }

        var returnUrl = Uri.EscapeDataString(requestPath + context.Request.QueryString);
        context.Response.Redirect("/Login?ReturnUrl=" + returnUrl);
    }
}
