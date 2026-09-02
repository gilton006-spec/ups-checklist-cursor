using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace UpsChecklist.Core;

public sealed class HandoverEmailSender
{
    public async Task SendAsync(
        byte[] pdfBytes,
        string filename,
        HandoverEmailOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("Email handover is not configured.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(options.Recipient));
        message.Subject = $"{EmailConstants.SubjectPrefix} - {filename}";

        var body = new BodyBuilder
        {
            TextBody = "Sunrise checklist report attached. This message was sent from the checklist prototype.",
        };
        body.Attachments.Add(filename, pdfBytes, new ContentType("application", "pdf"));
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.Username))
            await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
