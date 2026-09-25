using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Global customer account — one record per real human (by email).
/// Cross-tenant. Links all dealership-scoped profiles.
/// Email is always stored in normalized form (trimmed, lowercased).
/// Notification opt-outs are per dealership and live on <see cref="CustomerProfile"/> only.
///
/// Cosmos DB partition key: /email
/// </summary>
public class GlobalCustomerAcct : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "globalCustomerAcct";

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// All dealership-scoped profiles linked to this identity.
    /// Enables "show me all my service history across all dealerships."
    /// </summary>
    [JsonProperty("linkedProfiles")]
    public List<LinkedProfileEmbedded> LinkedProfiles { get; set; } = [];

    /// <summary>
    /// All asset identifiers ever associated with this person across all dealerships.
    /// </summary>
    [JsonProperty("allKnownAssetIds")]
    public List<string> AllKnownAssetIds { get; set; } = [];

    /// <summary>
    /// Global magic-link token — resolves to the identity (not a single profile).
    /// Status page shows requests across all dealerships.
    /// </summary>
    [JsonProperty("magicLinkToken")]
    public string? MagicLinkToken { get; set; }

    /// <summary>
    /// Expiration time for the magic-link token. Default 30 days, configurable per tenant.
    /// </summary>
    [JsonProperty("magicLinkExpiresAtUtc")]
    public DateTime? MagicLinkExpiresAtUtc { get; set; }

    /// <summary>
    /// Phase 2+: Auth0 user ID when customer creates an account.
    /// Null during MVP.
    /// </summary>
    [JsonProperty("auth0UserId")]
    public string? Auth0UserId { get; set; }

    /// <summary>
    /// The id a new account for <paramref name="email"/> is created with:
    /// <c>gca_</c> + lowercase hex SHA-256 of the normalised (trimmed, lowercased) email.
    /// The container has no unique key, so this is what makes a second create for the same
    /// email collide in Cosmos rather than add a duplicate (issue #679). Hashed because an
    /// email may contain characters a Cosmos id cannot (<c>/ \ ? #</c>). Accounts created
    /// before #679 keep their GUID ids; lookups go by email, never by this value.
    /// </summary>
    /// <param name="email">The customer's email, in any case or padding.</param>
    public static string IdForEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return $"gca_{Convert.ToHexStringLower(hash)}";
    }
}

// ---------------------------------------------------------------------------
// Embedded: LinkedProfileEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Lightweight pointer from a global customer account to a tenant-scoped profile.
/// </summary>
public class LinkedProfileEmbedded
{
    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonProperty("dealershipName")]
    public string DealershipName { get; set; } = string.Empty;

    [JsonProperty("firstSeenAtUtc")]
    public DateTime FirstSeenAtUtc { get; set; }

    [JsonProperty("requestCount")]
    public int RequestCount { get; set; }
}
