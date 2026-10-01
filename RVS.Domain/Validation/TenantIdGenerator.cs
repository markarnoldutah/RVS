namespace RVS.Domain.Validation;

/// <summary>
/// Derives tenant ids and the fixed ids of a tenant's first documents (Spec P-1 / P-6, issue #563).
///
/// Tenant ids are shaped <c>ten_{snake_name}</c> (e.g. <c>ten_nova_rv_services</c>) — an ordinary
/// string, not an Auth0 organization identifier. The dealership and first location get ids
/// derived from the tenant id rather than random GUIDs, so re-submitting a partially failed
/// provisioning finds the documents the first attempt wrote instead of duplicating them.
/// </summary>
public static class TenantIdGenerator
{
    /// <summary>Prefix every provisioned tenant id carries.</summary>
    public const string Prefix = "ten_";

    /// <summary>Longest tenant id accepted, prefix included.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Builds <c>ten_{snake_name}</c> from a display name: diacritics stripped, lowercased, every
    /// run of other characters collapsed to one underscore, capped at <see cref="MaxLength"/>.
    /// Returns an empty string when the name has no letters or digits.
    /// </summary>
    public static string FromName(string? name)
    {
        var slug = SlugGenerator.Slugify(name, MaxLength - Prefix.Length);

        return slug.Length == 0
            ? string.Empty
            : Prefix + slug.Replace('-', '_');
    }

    /// <summary>Fixed id of the dealership document created with a tenant, e.g. <c>dlr_nova_rv</c>.</summary>
    public static string DealershipIdFor(string tenantId) => $"dlr_{Suffix(tenantId)}";

    /// <summary>Fixed id of the first location created with a tenant, e.g. <c>loc_nova_rv_1</c>.</summary>
    public static string FirstLocationIdFor(string tenantId) => $"loc_{Suffix(tenantId)}_1";

    private static string Suffix(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return tenantId.StartsWith(Prefix, StringComparison.Ordinal)
            ? tenantId[Prefix.Length..]
            : tenantId;
    }
}
