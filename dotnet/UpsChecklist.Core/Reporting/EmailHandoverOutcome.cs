namespace UpsChecklist.Core.Reporting;

public sealed record EmailHandoverOutcome(
    string DisplayAddress,
    bool UsesTempInbox,
    string? DevInboxUrl)
{
    public string UserMessage => UsesTempInbox
        ? $"The test mail server accepted the PDF for {DisplayAddress}. This is not a live company inbox. Open Ethereal to check whether the message is there. Delivery is not confirmed."
        : "The mail server accepted the PDF for the configured inbox. Delivery to the recipient is not confirmed.";
}
