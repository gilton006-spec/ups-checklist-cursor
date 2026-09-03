using UpsChecklist.Core.Scanners;

namespace UpsChecklist.Core.Reporting;

public interface IScannerPdfCreator
{
    byte[] Create(ScannerSubmission data, string returnInstruction = "");
}
