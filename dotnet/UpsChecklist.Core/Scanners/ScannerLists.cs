using System.Globalization;
using System.Text.Json;

namespace UpsChecklist.Core.Scanners;

public sealed record ScannerInstruction(string Text, string SourceCell);
public sealed record ScannerRow(string Id, int SourceRow, string UserName, string Gost,
    string ScannerType, string Position, string HandoverTo, string ScannerNumber, string Note);
public sealed record ScannerSheet(string Id, string SourceSheet, string Title,
    IReadOnlyList<string> Headers, IReadOnlyList<ScannerRow> Rows, IReadOnlyList<ScannerInstruction> Instructions);
public sealed record ScannerVersion(string Id, string Label, string SourceFile, string SourceSha256,
    IReadOnlyList<ScannerSheet> Sheets);

public static class ScannerLists
{
    public static IReadOnlyList<ScannerVersion> All { get; } = Load();

    private static IReadOnlyList<ScannerVersion> Load()
    {
        using var stream = typeof(ScannerLists).Assembly.GetManifestResourceStream("scanner-lists.json")
            ?? throw new InvalidOperationException("Scanner source data is missing.");
        return JsonSerializer.Deserialize<ScannerVersion[]>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Scanner source data is empty.");
    }

    // The source includes weekdays only. Do not silently assign weekends to Friday.
    public static ScannerVersion? ForDate(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => All.Single(v => v.Id == "monday"),
        DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday
            => All.Single(v => v.Id == "tuesday-friday"),
        _ => null,
    };

    public static DateOnly ParseDate(string? value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ScannerValidationException("Choose a valid date.");
        return date;
    }
}

public sealed class ScannerEntry
{
    public string HandoverTo { get; set; } = "";
    public string ScannerNumber { get; set; } = "";
    public string Comments { get; set; } = "";
    // Only used where the paper leaves the position blank.
    public string Position { get; set; } = "";
}

public sealed class ScannerSubmission
{
    public string Date { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string SheetId { get; set; } = "";
    public Dictionary<string, ScannerEntry> Entries { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ScannerValidationException(string message) : Exception(message);

public static class ScannerValidator
{
    public const int MaxBodyBytes = 64 * 1024;
    public const int MaxHandoverLength = 80;
    public const int MaxScannerNumberLength = 40;
    public const int MaxPositionLength = 40;
    public const int MaxCommentsLength = 300;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 8,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public static ScannerSubmission ParseAndValidate(string json)
    {
        ScannerSubmission data;
        try
        {
            data = JsonSerializer.Deserialize<ScannerSubmission>(json, JsonOptions)
                ?? throw new ScannerValidationException("The scanner report is empty.");
        }
        catch (JsonException) { throw new ScannerValidationException("The scanner report is not valid JSON."); }
        Validate(data);
        return data;
    }

    public static (ScannerVersion Version, ScannerSheet Sheet) Validate(ScannerSubmission data)
    {
        var version = ScannerLists.ForDate(ScannerLists.ParseDate(data.Date))
            ?? throw new ScannerValidationException("No scanner list is supplied for Saturday or Sunday. Choose a weekday.");
        if (data.VersionId != version.Id)
            throw new ScannerValidationException("The scanner version does not match the date. Re-select the date.");
        var sheet = version.Sheets.SingleOrDefault(s => s.Id == data.SheetId)
            ?? throw new ScannerValidationException("Choose a valid scanner list.");
        if (data.Entries is null || data.Entries.Count > sheet.Rows.Count)
            throw new ScannerValidationException("The scanner entries do not match this list.");
        foreach (var (id, entry) in data.Entries)
        {
            var row = sheet.Rows.SingleOrDefault(r => r.Id == id);
            if (row is null || entry is null)
                throw new ScannerValidationException("An entry does not belong to this scanner list.");
            Check(entry.HandoverTo, MaxHandoverLength, "Handover to");
            Check(entry.ScannerNumber, MaxScannerNumberLength, "Scanner number");
            Check(entry.Comments, MaxCommentsLength, "Comments", multiline: true);
            if (entry.Comments.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Length > 20)
                throw new ScannerValidationException("Comments: use at most 20 lines per scanner.");
            Check(entry.Position, MaxPositionLength, "Position");
            if (row.Position.Length > 0 && entry.Position.Length > 0 && entry.Position != row.Position)
                throw new ScannerValidationException("A pre-assigned position cannot be changed.");
        }
        return (version, sheet);
    }

    private static void Check(string? value, int max, string label, bool multiline = false)
    {
        if (value is null || value.Length > max || value.Any(c => char.IsControl(c) && !(multiline && c is '\n' or '\r')))
            throw new ScannerValidationException($"{label}: use at most {max} characters without control characters.");
    }
}
