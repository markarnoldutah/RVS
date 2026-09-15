namespace RVS.API.Options;

/// <summary>
/// Platform-admin settings, bound from the <c>Admin</c> section (Spec P-7, issue #563).
/// In Azure the values come from Key Vault as <c>Admin--AllowedUserIds--0</c>, <c>--1</c>, ….
/// </summary>
public sealed class AdminOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Admin";

    /// <summary>
    /// Auth0 user ids (<c>sub</c>) allowed through the <c>PlatformAdmin</c> policy, in addition to
    /// the <c>platform:tenants:manage</c> permission. Empty means nobody.
    /// </summary>
    public List<string> AllowedUserIds { get; set; } = [];
}
