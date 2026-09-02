namespace UpsChecklist.Core.Models;

public sealed record CheckItem(string Id, string Text, string? Note, string SourceCell);

public sealed record ReferenceImage(string File, string Label);

public sealed record CheckSection(
    string Id,
    string Title,
    IReadOnlyList<CheckItem> Items,
    IReadOnlyList<ReferenceImage>? Images = null,
    string? Note = null,
    string? RemarksKey = null);

public sealed record Position(
    string Id,
    string Label,
    string Title,
    string SourceSheet,
    IReadOnlyList<CheckSection> Sections,
    string PackageLabel,
    string? Schedule = null,
    string? ScheduleLabel = null,
    string? Handover = null,
    IReadOnlyList<string>? SourceNotes = null);

public sealed class ChecklistSubmission
{
    public required string PositionId { get; init; }
    public string Date { get; init; } = "";
    public string Name { get; init; } = "";
    public Dictionary<string, bool> Checks { get; init; } = new();
    public string Count { get; init; } = "";
    public string Remarks { get; init; } = "";
    public Dictionary<string, string> SectionRemarks { get; init; } = new();
    public string EvidencePhoto { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Drawn { get; init; } = "";
}

public sealed class WorkbookSource
{
    public required string Source { get; init; }
    public required List<WorkbookSheet> Sheets { get; init; }
}

public sealed class WorkbookSheet
{
    public required string Name { get; init; }
    public required Dictionary<string, string> Cells { get; init; }
    public required List<WorkbookImageRef> Images { get; init; }
}

public sealed class WorkbookImageRef
{
    public required string File { get; init; }
    public int Row { get; init; }
}
