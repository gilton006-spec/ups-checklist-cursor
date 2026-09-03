using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using UpsChecklist.Core;

namespace UpsChecklist.Web.Pages;

[AllowAnonymous]
public sealed class LoginModel(IOptions<SiteAccessOptions> accessOptions) : PageModel
{
    private readonly SiteAccessOptions _access = accessOptions.Value;

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!_access.IsEnabled)
            return Redirect(SafeReturnUrl(returnUrl));

        if (User.Identity?.IsAuthenticated == true)
            return Redirect(SafeReturnUrl(returnUrl));

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!_access.IsEnabled)
            return Redirect(SafeReturnUrl(returnUrl));

        var entered = (Password ?? "").Trim().ToLowerInvariant();
        var expected = _access.Password.Trim().ToLowerInvariant();
        if (entered.Length == 0 || !FixedTimeEquals(entered, expected))
        {
            ErrorMessage = "Wrong password. Try again.";
            Password = "";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "shift")],
            SiteAccessOptions.CookieScheme);
        await HttpContext.SignInAsync(
            SiteAccessOptions.CookieScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
                AllowRefresh = true,
            });

        return Redirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return returnUrl;
        return "/";
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(a),
            SHA256.HashData(b));
    }
}
