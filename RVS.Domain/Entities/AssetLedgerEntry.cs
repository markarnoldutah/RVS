using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Append-only service event record linked to a VIN.
/// One entry per service request, written at intake time.
/// This is the data moat: proprietary, accumulating, non-replicable data
/// that powers Section 10A service intelligence.
///
/// Cosmos DB partition key: /vin
/// Write-once pattern — fields are immutable after creation except for
/// Section 10A fields which are enriched progressively.
/// </summary>
public class AssetLedgerEntry
{
    [JsonProperty("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("vin")]
    public string Vin { get; set; } = string.Empty;

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("dealershipName")]
    public string DealershipName { get; set; } = string.Empty;

    [JsonProperty("serviceRequestId")]
    public string ServiceRequestId { get; set; } = string.Empty;

    [JsonProperty("customerIdentityId")]
    public string CustomerIdentityId { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    [JsonProperty("issueCategory")]
    public string? IssueCategory { get; set; }

    [JsonProperty("issueDescription")]
    public string? IssueDescription { get; set; }

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
    public DateTime SubmittedAtUtc { get; set; }

    [JsonProperty("serviceDateUtc")]
    public DateTime? ServiceDateUtc { get; set; }
}
