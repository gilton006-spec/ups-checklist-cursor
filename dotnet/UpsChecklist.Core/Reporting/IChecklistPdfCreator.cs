using UpsChecklist.Core.Models;

namespace UpsChecklist.Core.Reporting;

public interface IChecklistPdfCreator
{
    byte[] Create(ChecklistSubmission data);
}
