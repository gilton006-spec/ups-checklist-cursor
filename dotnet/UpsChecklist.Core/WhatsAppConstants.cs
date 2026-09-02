namespace UpsChecklist.Core;

public static class WhatsAppConstants
{
    public const string HandoverNumber = "+31 626149058";
    public static readonly string HandoverChatUrl =
        "https://wa.me/31626149058?text=" +
        Uri.EscapeDataString("Sunrise checklist. I will attach the report PDF here.");

    public static string ReportFilename(string positionId, string date)
    {
        var safeId = new string(positionId.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        var safeDate = new string(date.Where(c => char.IsDigit(c) || c == '-').ToArray());
        return $"UPS_{safeId}_{(string.IsNullOrEmpty(safeDate) ? "undated" : safeDate)}.pdf";
    }
}
