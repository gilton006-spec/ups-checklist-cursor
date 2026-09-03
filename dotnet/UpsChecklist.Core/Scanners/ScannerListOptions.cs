namespace UpsChecklist.Core.Scanners;

public sealed class ScannerListOptions
{
    // Visible on the scanner page and every PDF page. Confirm the drop-off location with the team leader if it should name a place.
    public string ReturnInstruction { get; set; } = "";
}
