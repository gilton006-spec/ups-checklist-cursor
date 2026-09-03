using Microsoft.Extensions.Options;
using UpsChecklist.Core;

namespace UpsChecklist.Web.Http;

public sealed class SiteAccessMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AnonymousPaths =
    [
        "/Login",
        "/Error",
        "/health",
    ];

    public async Task InvokeAsync(HttpContext context, IOptions<SiteAccessOptions> accessOptions)
    {
        var access = accessOptions.Value;
        if (!access.IsEnabled)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path;
        if (AnonymousPaths.Any(p => path.StartsWithSegments(p)))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        if (path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Sign in required.");
            return;
        }

        var returnUrl = Uri.EscapeDataString(path + context.Request.QueryString);
        context.Response.Redirect("/Login?ReturnUrl=" + returnUrl);
    }
}
