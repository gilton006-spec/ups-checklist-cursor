namespace UpsChecklist.Core;

public static class EmailConstants
{
    public const string HandoverAddress = "gilton93@hotmail.com";
    public const string SubjectPrefix = "Sunrise checklist";

    public static string MailtoUrl(string filename) =>
        $"mailto:{HandoverAddress}?subject={Uri.EscapeDataString($"{SubjectPrefix} - {filename}")}" +
        $"&body={Uri.EscapeDataString($"Sunrise checklist report: {filename}. Please attach the downloaded PDF before sending.")}";
}
