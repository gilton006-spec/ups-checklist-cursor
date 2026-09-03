using UpsChecklist.Core;
using UpsChecklist.Core.Reporting;

namespace UpsChecklist.Tests;

public sealed class ReportSubmissionGuardTests
{
    [Fact]
    public void Duplicate_payloads_are_rejected_inside_the_window_then_released()
    {
        var guard = new ReportSubmissionGuard(TimeSpan.FromMinutes(1));
        Assert.True(guard.TryAccept("same"));
        Assert.False(guard.TryAccept("same"));
        Assert.True(guard.TryAccept("other"));
        guard.Release("same");
        Assert.True(guard.TryAccept("same"));
    }
}

public sealed class ChecklistNullJsonTests
{
    [Fact]
    public void Explicit_null_and_malformed_json_have_distinct_errors()
    {
        var malformed = Assert.Throws<ChecklistValidationException>(() => ChecklistValidator.ParseAndValidate("{"));
        Assert.Contains("JSON", malformed.Message);

        var missing = Assert.Throws<ChecklistValidationException>(() => ChecklistValidator.ParseAndValidate(""));
        Assert.Contains("missing", missing.Message);

        var nullChecks = Assert.Throws<ChecklistValidationException>(() => ChecklistValidator.ParseAndValidate(
            """{"positionId":"pd4","date":"2026-09-02","name":"QA","checks":null,"count":"1","remarks":"","sectionRemarks":{},"signature":"QA","drawn":""}"""));
        Assert.Contains("empty required field", nullChecks.Message);
    }

    [Fact]
    public void Unchecked_items_stay_false()
    {
        var position = ChecklistPositions.Get("pd4")!;
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["positionId"] = position.Id,
            ["date"] = "2026-09-02",
            ["name"] = "QA",
            ["checks"] = new Dictionary<string, bool>(),
            ["count"] = "",
            ["remarks"] = "",
            ["sectionRemarks"] = new Dictionary<string, string>(),
            ["signature"] = "",
            ["drawn"] = "",
        });
        var data = ChecklistValidator.ParseAndValidate(json);
        foreach (var item in ChecklistPositions.AllItems(position))
            Assert.False(data.Checks.GetValueOrDefault(item.Id));
    }
}
