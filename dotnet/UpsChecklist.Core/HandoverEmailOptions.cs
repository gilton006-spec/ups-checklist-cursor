namespace UpsChecklist.Core;

public sealed class HandoverEmailOptions
{
    public const string SectionName = "HandoverEmail";

    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string From { get; init; } = "";
    public string To { get; init; } = "";

    public string Recipient =>
        string.IsNullOrWhiteSpace(To) ? EmailConstants.HandoverAddress : To;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(From)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password)
        && Password != "PASTE_YOUR_HOTMAIL_APP_PASSWORD_HERE";
}
