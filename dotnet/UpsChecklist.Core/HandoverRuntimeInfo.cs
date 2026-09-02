namespace UpsChecklist.Core;

public sealed class HandoverRuntimeInfo
{
    public required string DisplayAddress { get; init; }
    public string? DevInboxUrl { get; init; }
    public bool UsesTempInbox { get; init; }

    public static HandoverRuntimeInfo Production() => ForOptions(new HandoverEmailOptions());

    public static HandoverRuntimeInfo ForOptions(HandoverEmailOptions options) => new()
    {
        DisplayAddress = options.Recipient,
        DevInboxUrl = IsEthereal(options) ? "https://ethereal.email" : null,
        UsesTempInbox = IsEthereal(options),
    };

    public static bool IsEthereal(HandoverEmailOptions options) =>
        options.Host.Contains("ethereal", StringComparison.OrdinalIgnoreCase)
        || options.Username.Contains("ethereal.email", StringComparison.OrdinalIgnoreCase);
}
