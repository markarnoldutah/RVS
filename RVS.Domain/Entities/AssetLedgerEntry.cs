using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Append-only service event record linked to a VIN.
/// One entry per service request, written at intake time.
/// This is the data moat: proprietary, accumulating, non-replicable data
/// that powers Section 10A service intelligence.
///
/// Cosmos DB partition key: /vin
/// Write-once pattern — core fields are immutable after creation.
/// Section 10A fields are enriched progressively via change feed.
/// </summary>
public class AssetLedgerEntry
{
    [JsonProperty("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Vehicle Identification Number. Partition key — immutable after creation.
    /// </summary>
    [JsonProperty("vin")]
    public string Vin { get; init; } = string.Empty;

    /// <summary>
    /// Tenant (dealership) that created this ledger entry. Immutable after creation.
    /// </summary>
    [JsonProperty("tenantId")]
    public string TenantId { get; init; } = string.Empty;

    [JsonProperty("dealershipName")]
    public string DealershipName { get; init; } = string.Empty;

    /// <summary>
    /// Cross-reference to the ServiceRequest that generated this entry. Immutable after creation.
    /// </summary>
    [JsonProperty("serviceRequestId")]
    public string ServiceRequestId { get; init; } = string.Empty;

    [JsonProperty("customerIdentityId")]
    public string CustomerIdentityId { get; init; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; init; }

    [JsonProperty("model")]
    public string? Model { get; init; }

    [JsonProperty("year")]
    public int? Year { get; init; }

    [JsonProperty("issueCategory")]
    public string? IssueCategory { get; init; }

    [JsonProperty("issueDescription")]
    public string? IssueDescription { get; init; }

    // ── Section 10A fields — populated progressively ──

    [JsonProperty("failureMode")]
    public string? FailureMode { get; set; }

    [JsonProperty("repairAction")]
    public string? RepairAction { get; set; }

    [JsonProperty("partsUsed")]
    public List<string> PartsUsed { get; set; } = [];

    [JsonProperty("laborHours")]
    public decimal? LaborHours { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "New";

    [JsonProperty("submittedAtUtc")]
    public DateTime SubmittedAtUtc { get; init; }

    [JsonProperty("serviceDateUtc")]
    public DateTime? ServiceDateUtc { get; set; }
}
