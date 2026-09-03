using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using UpsChecklist.Core;

namespace UpsChecklist.Tests;

public sealed class SiteAccessTests
{
    [Fact]
    public async Task Unauthenticated_request_redirects_to_login_when_password_is_set()
    {
        await using var factory = new PasswordProtectedFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lowercase_password_grants_access_and_uppercase_variant_also_works_after_normalize()
    {
        await using var factory = new PasswordProtectedFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getLogin = await client.GetAsync("/Login");
        Assert.Equal(HttpStatusCode.OK, getLogin.StatusCode);
        var html = await getLogin.Content.ReadAsStringAsync();
        Assert.Contains("text-transform: lowercase", html, StringComparison.Ordinal);
        Assert.Contains("input.value = input.value.toLowerCase()", html, StringComparison.Ordinal);

        var token = ExtractAntiForgery(html);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Password"] = "EINDHOVEN",
            ["__RequestVerificationToken"] = token,
        });
        var post = await client.PostAsync("/Login", form);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal("/", post.Headers.Location?.OriginalString);

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }

    [Fact]
    public async Task Sign_out_clears_access_cookie()
    {
        await using var factory = new PasswordProtectedFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginHtml = await (await client.GetAsync("/Login")).Content.ReadAsStringAsync();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Password"] = "eindhoven",
            ["__RequestVerificationToken"] = ExtractAntiForgery(loginHtml),
        });
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Login", form)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var homeHtml = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        using var logout = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ExtractAntiForgery(homeHtml),
        });
        var logoutResponse = await client.PostAsync("/Logout", logout);
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        var blocked = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("/Login", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_like_hosts_fail_closed_without_password()
    {
        var options = new SiteAccessOptions { Password = "", AllowOpenAccess = false };
        Assert.True(options.IsMisconfigured(isProductionLike: true));
        Assert.False(options.IsMisconfigured(isProductionLike: false));

        options.AllowOpenAccess = true;
        Assert.False(options.IsMisconfigured(isProductionLike: true));

        options.AllowOpenAccess = false;
        options.Password = "secret";
        Assert.False(options.IsMisconfigured(isProductionLike: true));
    }

    private static string ExtractAntiForgery(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            const string alt = "name=\"__RequestVerificationToken\" value=\"";
            start = html.IndexOf(alt, StringComparison.Ordinal);
            Assert.True(start >= 0, "Anti-forgery token missing.");
            start += alt.Length;
        }
        else
        {
            start += marker.Length;
        }

        var end = html.IndexOf('"', start);
        return html[start..end];
    }

    private sealed class PasswordProtectedFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SiteAccess:Password"] = "eindhoven",
                });
            });
        }
    }
}
