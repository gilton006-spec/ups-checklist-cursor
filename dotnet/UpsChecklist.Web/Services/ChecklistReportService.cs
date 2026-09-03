using Microsoft.Extensions.Options;
using UpsChecklist.Core;
using UpsChecklist.Core.Reporting;

namespace UpsChecklist.Web.Services;

public sealed class ChecklistReportService(
    IChecklistPdfCreator pdf,
    IHandoverEmailSender email,
    IOptions<HandoverEmailOptions> emailOptions,
    HandoverRuntimeInfo runtime,
    ReportSubmissionGuard duplicates,
    ILogger<ChecklistReportService> logger)
{
    public static readonly EventId PdfFailed = new(2101, nameof(PdfFailed));
    public static readonly EventId EmailFailed = new(2102, nameof(EmailFailed));
    public static readonly EventId EmailAccepted = new(2103, nameof(EmailAccepted));

    public bool EmailIsConfigured => emailOptions.Value.IsConfigured;

    public PdfCreateResult CreatePdfReport(string payload)
    {
        try
        {
            var data = ChecklistValidator.ParseAndValidate(payload);
            var bytes = pdf.Create(data);
            var filename = WhatsAppConstants.ReportFilename(data.PositionId, data.Date);
            return new PdfCreateResult.Ok(new PdfReportResult(bytes, filename));
        }
        catch (ChecklistValidationException ex)
        {
            return new PdfCreateResult.Invalid(ex.Message);
        }
        catch (Exception ex) when (IsFontLimitation(ex))
        {
            return new PdfCreateResult.FontRejected(
                "Please go back and use standard Latin letters in the text fields, or draw your signature.");
        }
        catch (Exception ex)
        {
            logger.LogError(PdfFailed, ex, "Checklist PDF generation failed.");
            return new PdfCreateResult.Failed(
                "The PDF could not be created. Your checklist is still open. Try again.");
        }
    }

    /// <summary>
    /// Full email handover: reserve → PDF → SMTP. Releases the reservation only when
    /// the mail transport was never contacted.
    /// </summary>
    public async Task<EmailHandoverResult> SendEmailHandoverAsync(string payload, CancellationToken cancellationToken)
    {
        if (!EmailIsConfigured)
            return new EmailHandoverResult.NotConfigured();

        if (!duplicates.TryReserve(payload))
            return new EmailHandoverResult.Duplicate();

        var contactedTransport = false;
        try
        {
            var data = ChecklistValidator.ParseAndValidate(payload);
            var bytes = pdf.Create(data);
            var filename = WhatsAppConstants.ReportFilename(data.PositionId, data.Date);

            contactedTransport = true;
            await email.SendAsync(bytes, filename, cancellationToken);
            logger.LogInformation(EmailAccepted, "Handover email accepted by transport. TestInbox={TestInbox}", runtime.UsesTempInbox);
            return new EmailHandoverResult.Accepted(
                new EmailHandoverOutcome(runtime.DisplayAddress, runtime.UsesTempInbox, runtime.DevInboxUrl));
        }
        catch (ChecklistValidationException ex)
        {
            duplicates.ReleaseAfterDefiniteFailure(payload);
            return new EmailHandoverResult.Invalid(ex.Message);
        }
        catch (Exception ex) when (IsFontLimitation(ex))
        {
            duplicates.ReleaseAfterDefiniteFailure(payload);
            return new EmailHandoverResult.FontRejected(
                "Please go back and use standard Latin letters in the text fields, or draw your signature.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client abandoned the request; keep reservation — outcome is unknown.
            throw;
        }
        catch (Exception ex) when (!contactedTransport)
        {
            duplicates.ReleaseAfterDefiniteFailure(payload);
            logger.LogError(PdfFailed, ex, "Checklist PDF generation failed before email send.");
            return new EmailHandoverResult.FailedBeforeSend(
                "The PDF could not be created. Your checklist is still open. Try again.");
        }
        catch (Exception ex)
        {
            // Do not release: SMTP may have accepted the message.
            logger.LogError(EmailFailed, ex, "Handover email failed after contacting the mail transport.");
            return new EmailHandoverResult.OutcomeUnknown(
                "The email could not be handed to the mail server. Your checklist is still open. Whether anything arrived is unknown; check before retrying.");
        }
    }

    private static bool IsFontLimitation(Exception ex) =>
        ex.Message.Contains("WinAnsi", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("does not support U+", StringComparison.Ordinal);
}
