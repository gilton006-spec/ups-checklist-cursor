using Microsoft.Extensions.Options;
using UpsChecklist.Core;
using UpsChecklist.Core.Models;
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

    public ChecklistSubmission Parse(string json) => ChecklistValidator.ParseAndValidate(json);

    public byte[] CreatePdf(ChecklistSubmission data) => pdf.Create(data);

    public bool EmailIsConfigured => emailOptions.Value.IsConfigured;

    public bool TryAcceptSubmission(string payload) => duplicates.TryAccept(payload);

    public void ReleaseSubmission(string payload) => duplicates.Release(payload);

    public async Task<EmailHandoverOutcome> SendEmailAsync(byte[] pdfBytes, string filename, CancellationToken cancellationToken)
    {
        if (!EmailIsConfigured)
            throw new InvalidOperationException("Email handover is not configured.");

        await email.SendAsync(pdfBytes, filename, cancellationToken);
        logger.LogInformation(EmailAccepted, "Handover email accepted by transport. TestInbox={TestInbox}", runtime.UsesTempInbox);
        return new EmailHandoverOutcome(runtime.DisplayAddress, runtime.UsesTempInbox, runtime.DevInboxUrl);
    }

    public void LogPdfFailure(Exception exception) =>
        logger.LogError(PdfFailed, exception, "Checklist PDF generation failed.");

    public void LogEmailFailure(Exception exception) =>
        logger.LogError(EmailFailed, exception, "Handover email failed after PDF generation.");
}
