using System.Net.Http.Json;
using System.Text.Json.Serialization;
using UpsChecklist.Core;

namespace UpsChecklist.Web.Infrastructure;

/// <summary>
/// Creates a temporary Ethereal inbox for local Development when SMTP is not configured.
/// Lives in Web because Core must not perform HTTP calls.
/// </summary>
public static class EtherealEmailProvisioner
{
    public sealed record TempInbox(
        HandoverEmailOptions Options,
        string InboxAddress,
        string InboxUrl,
        string Username,
        string Password);

    public static async Task<TempInbox> ProvisionAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await client.PostAsJsonAsync(
            "https://api.nodemailer.com/user",
            new { requestor = "UpsChecklist", version = "1.0.0" },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<EtherealAccountResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Could not create a temporary email account.");

        var inboxUrl = string.IsNullOrWhiteSpace(account.Web)
            ? "https://ethereal.email/login"
            : account.Web;

        var options = new HandoverEmailOptions
        {
            Host = account.Smtp.Host,
            Port = account.Smtp.Port,
            Username = account.User,
            Password = account.Pass,
            From = account.User,
            To = account.User,
        };

        return new TempInbox(
            options,
            account.User,
            inboxUrl,
            account.User,
            account.Pass);
    }

    private sealed class EtherealAccountResponse
    {
        [JsonPropertyName("user")]
        public required string User { get; init; }

        [JsonPropertyName("pass")]
        public required string Pass { get; init; }

        [JsonPropertyName("web")]
        public string? Web { get; init; }

        [JsonPropertyName("smtp")]
        public required EtherealSmtp Smtp { get; init; }
    }

    private sealed class EtherealSmtp
    {
        [JsonPropertyName("host")]
        public required string Host { get; init; }

        [JsonPropertyName("port")]
        public required int Port { get; init; }
    }
}
