using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UpsChecklist.Core;

namespace UpsChecklist.Web.Pages;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Login");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(SiteAccessOptions.CookieScheme);
        return RedirectToPage("/Login");
    }
}
