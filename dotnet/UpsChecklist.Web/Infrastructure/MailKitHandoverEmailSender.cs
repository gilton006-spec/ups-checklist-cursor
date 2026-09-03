using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using UpsChecklist.Core;
using UpsChecklist.Core.Reporting;

namespace UpsChecklist.Web.Infrastructure;

public sealed class MailKitHandoverEmailSender(IOptions<HandoverEmailOptions> options) : IHandoverEmailSender
{
    public async Task SendAsync(byte[] pdfBytes, string filename, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new InvalidOperationException("Email handover is not configured.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(settings.Recipient));
        message.Subject = $"{EmailConstants.SubjectPrefix} - {filename}";

        var body = new BodyBuilder
        {
            TextBody = "Sunrise checklist report attached. This message was sent from the checklist prototype.",
        };
        body.Attachments.Add(filename, pdfBytes, new ContentType("application", "pdf"));
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
