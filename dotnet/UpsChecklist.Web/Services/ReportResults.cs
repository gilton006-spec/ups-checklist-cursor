using UpsChecklist.Core.Reporting;

namespace UpsChecklist.Web.Services;

public abstract record EmailHandoverResult
{
    public sealed record NotConfigured : EmailHandoverResult;
    public sealed record Duplicate : EmailHandoverResult;
    public sealed record Accepted(EmailHandoverOutcome Outcome) : EmailHandoverResult;
    public sealed record Invalid(string Message) : EmailHandoverResult;
    public sealed record FontRejected(string Message) : EmailHandoverResult;
    /// <summary>Work failed before SMTP was contacted; reservation was released.</summary>
    public sealed record FailedBeforeSend(string Message) : EmailHandoverResult;
    /// <summary>SMTP may have been contacted; reservation was kept.</summary>
    public sealed record OutcomeUnknown(string Message) : EmailHandoverResult;
}

public sealed record PdfReportResult(byte[] Bytes, string Filename);

public abstract record PdfCreateResult
{
    public sealed record Ok(PdfReportResult Report) : PdfCreateResult;
    public sealed record Invalid(string Message) : PdfCreateResult;
    public sealed record FontRejected(string Message) : PdfCreateResult;
    public sealed record Failed(string Message) : PdfCreateResult;
}
