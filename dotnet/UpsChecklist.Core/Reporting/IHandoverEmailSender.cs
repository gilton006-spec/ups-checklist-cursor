namespace UpsChecklist.Core.Reporting;

public interface IHandoverEmailSender
{
    Task SendAsync(byte[] pdfBytes, string filename, CancellationToken cancellationToken = default);
}
