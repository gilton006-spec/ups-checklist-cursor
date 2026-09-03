using System.Text.Json;
using System.Text.RegularExpressions;
using UpsChecklist.Core.Models;

namespace UpsChecklist.Core;

public static partial class ChecklistValidator
{
    public const int MaxBodyBytes = 3_200_000;
    public const int MaxPayloadChars = 2_100_000;

    [GeneratedRegex(@"^\d{0,5}$")]
    private static partial Regex CountPattern();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})?$")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^data:image/png;base64,[A-Za-z0-9+/=]+$")]
    private static partial Regex DrawnPattern();

    public static ChecklistSubmission ParseAndValidate(string rawJson)
    {
        if (string.IsNullOrEmpty(rawJson))
            throw new ChecklistValidationException("The checklist is missing.");
        if (rawJson.Length > MaxPayloadChars)
            throw new ChecklistValidationException("The report is too large. Choose a smaller photo and try again.");

        ChecklistSubmission? data;
        try
        {
            data = JsonSerializer.Deserialize<ChecklistSubmission>(rawJson, JsonOptions);
        }
        catch (JsonException)
        {
            throw new ChecklistValidationException("The checklist is not valid JSON.");
        }

        if (data is null)
            throw new ChecklistValidationException("The checklist is missing.");

        if (data.PositionId is null
            || data.Date is null
            || data.Name is null
            || data.Count is null
            || data.Remarks is null
            || data.Signature is null
            || data.Drawn is null
            || data.BeforeSortEvidencePhoto is null
            || data.EvidencePhoto is null
            || data.Checks is null
            || data.SectionRemarks is null)
        {
            throw new ChecklistValidationException("The checklist contains an empty required field.");
        }

        Validate(data);
        return data;
    }

    public static void Validate(ChecklistSubmission data)
    {
        var position = ChecklistPositions.Get(data.PositionId)
            ?? throw new ChecklistValidationException("Select a valid position");

        if (!DatePattern().IsMatch(data.Date))
            throw new ChecklistValidationException("Invalid date.");
        if (!string.IsNullOrEmpty(data.Date))
        {
            if (!DateTime.TryParse(data.Date, out var parsed) || parsed.ToString("yyyy-MM-dd") != data.Date)
                throw new ChecklistValidationException("Invalid date.");
        }

        if (data.Name.Length > 65)
            throw new ChecklistValidationException("Name is too long.");
        if (!CountPattern().IsMatch(data.Count))
            throw new ChecklistValidationException("Invalid package count.");
        if (data.Remarks.Length > 2000)
            throw new ChecklistValidationException("Remarks are too long.");
        if (data.Signature.Length > 65)
            throw new ChecklistValidationException("Signature is too long.");
        if (data.Drawn.Length > 200_000 || (data.Drawn.Length > 0 && !DrawnPattern().IsMatch(data.Drawn)))
            throw new ChecklistValidationException("Invalid drawn signature.");
        if (!EvidenceValidation.ValidEvidencePhoto(data.BeforeSortEvidencePhoto)
            || !EvidenceValidation.ValidEvidencePhoto(data.EvidencePhoto))
            throw new ChecklistValidationException("Invalid evidence photo.");

        foreach (var remark in data.SectionRemarks.Values)
        {
            if (remark.Length > 1000)
                throw new ChecklistValidationException("Section remarks are too long.");
        }

        var allowedChecks = new HashSet<string>(ChecklistPositions.AllItems(position).Select(i => i.Id));
        var allowedRemarks = new HashSet<string>(
            position.Sections.Where(s => s.RemarksKey is not null).Select(s => s.RemarksKey!));

        if (data.Checks.Keys.Any(k => !allowedChecks.Contains(k))
            || data.SectionRemarks.Keys.Any(k => !allowedRemarks.Contains(k)))
        {
            throw new ChecklistValidationException("Entries do not match this position");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class ChecklistValidationException(string message) : Exception(message);
