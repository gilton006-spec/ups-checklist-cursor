namespace UpsChecklist.Core;

public sealed class SiteAccessOptions
{
    public const string SectionName = "SiteAccess";
    public const string CookieScheme = "SiteAccess";
    public const string CookieName = "ups_site_access";

    /// <summary>Shared shift password. Empty disables the login gate.</summary>
    public string Password { get; set; } = "";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Password);
}
