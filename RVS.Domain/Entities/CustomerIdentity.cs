using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Global customer identity — one record per real human (by email).
/// Cross-tenant. Links all dealership-scoped profiles.
/// Partitioned by normalizedEmail for O(1) intake resolution.
///
/// Cosmos DB partition key: /normalizedEmail
/// </summary>
public class CustomerIdentity : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "customerIdentity";

    [JsonProperty("normalizedEmail")]
    public string NormalizedEmail { get; set; } = string.Empty;

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
    /// All VINs ever associated with this person across all dealerships.
    /// </summary>
    [JsonProperty("allKnownVins")]
    public List<string> AllKnownVins { get; set; } = [];

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
}

// ---------------------------------------------------------------------------
// Embedded: LinkedProfileEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Lightweight pointer from a global identity to a tenant-scoped profile.
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
