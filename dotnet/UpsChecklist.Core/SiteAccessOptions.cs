namespace UpsChecklist.Core;

public sealed class SiteAccessOptions
{
    public const string SectionName = "SiteAccess";
    public const string CookieScheme = "SiteAccess";
    public const string CookieName = "ups_site_access";

    /// <summary>Shared shift password. Compared as lowercased trimmed text.</summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// When true, an empty password leaves the app open. Must be set explicitly.
    /// Production-like hosts refuse to start without a password unless this is true.
    /// </summary>
    public bool AllowOpenAccess { get; set; }

    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    /// <summary>Gate is active only when a password is configured.</summary>
    public bool IsEnabled => HasPassword;

    /// <summary>
    /// Production/Staging/Fly refuse to serve without a password unless open access is explicit.
    /// </summary>
    public bool IsMisconfigured(bool isProductionLike) =>
        isProductionLike && !HasPassword && !AllowOpenAccess;
}
