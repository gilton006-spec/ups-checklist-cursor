using System.Net;
using System.Text.RegularExpressions;

namespace UpsChecklist.Tests;

internal static class AntiforgeryForms
{
    public static async Task<string> TokenAsync(HttpClient client, string page = "/")
    {
        var html = await client.GetStringAsync(page);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Anti-forgery token missing on {page}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<FormUrlEncodedContent> ChecklistFormAsync(HttpClient client, string json)
    {
        var token = await TokenAsync(client);
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["checklist"] = json,
            ["__RequestVerificationToken"] = token,
        });
    }
}
