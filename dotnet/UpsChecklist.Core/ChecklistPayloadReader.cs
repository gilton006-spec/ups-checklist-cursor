using System.Text;

namespace UpsChecklist.Core;

public static class ChecklistPayloadReader
{
    public static string? ExtractFromBody(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        if (contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            return body;

        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
                continue;
            var key = Uri.UnescapeDataString(part[..idx]);
            if (key == "checklist")
                return Uri.UnescapeDataString(part[(idx + 1)..].Replace('+', ' '));
        }

        return null;
    }
}
