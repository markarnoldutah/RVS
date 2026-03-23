using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Service request aggregate root — represents a customer's intake submission
/// at a dealership location. Contains embedded snapshots of customer info,
/// vehicle details, attachments, service event data, and diagnostic responses.
///
/// Cosmos DB partition key: /tenantId
/// </summary>
public class ServiceRequest : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "serviceRequest";

    /// <summary>
    /// Unique identifier with <c>sr_</c> prefix convention for service requests.
    /// </summary>
    [JsonProperty("id")]
    public new string Id { get; init; } = $"sr_{Guid.NewGuid()}";

    /// <summary>
    /// Current workflow status. Transitions enforced by <see cref="Validation.StatusTransitions"/>.
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = "New";

    /// <summary>
    /// Reference to the location where the service request was submitted.
    /// </summary>
    [JsonProperty("locationId")]
    public string LocationId { get; init; } = string.Empty;

    /// <summary>
    /// Reference to the tenant-scoped customer profile.
    /// </summary>
    [JsonProperty("customerProfileId")]
    public string CustomerProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Point-in-time snapshot of customer info.
    /// Denormalized so dealer dashboard never joins to customer-profiles.
    /// </summary>
    [JsonProperty("customerSnapshot")]
    public CustomerSnapshotEmbedded CustomerSnapshot { get; set; } = new();

    /// <summary>
    /// Asset (vehicle/RV) information associated with this service request.
    /// </summary>
    [JsonProperty("assetInfo")]
    public AssetInfoEmbedded AssetInfo { get; set; } = new();

    /// <summary>
    /// Customer-provided description of the issue.
    /// </summary>
    [JsonProperty("issueDescription")]
    public string IssueDescription { get; set; } = string.Empty;

    /// <summary>
    /// Category of the issue from a LookupSet.
    /// </summary>
    [JsonProperty("issueCategory")]
    public string? IssueCategory { get; set; }

    /// <summary>
    /// AI-generated or advisor-written summary for the technician.
    /// </summary>
    [JsonProperty("technicianSummary")]
    public string? TechnicianSummary { get; set; }

    /// <summary>
    /// File attachments uploaded during intake (photos, videos, voice notes).
    /// </summary>
    [JsonProperty("attachments")]
    public List<ServiceRequestAttachmentEmbedded> Attachments { get; set; } = [];

    /// <summary>
    /// Structured service event data per Section 10A.
    /// Null until service work begins.
    /// </summary>
    [JsonProperty("serviceEvent")]
    public ServiceEventEmbedded? ServiceEvent { get; set; }

    /// <summary>
    /// AI-generated diagnostic question responses from the intake wizard.
    /// </summary>
    [JsonProperty("diagnosticResponses")]
    public List<DiagnosticResponseEmbedded> DiagnosticResponses { get; set; } = [];

    /// <summary>
    /// Scheduled service date. Null until an advisor schedules the request.
    /// </summary>
    [JsonProperty("scheduledDateUtc")]
    public DateTime? ScheduledDateUtc { get; set; }

    /// <summary>
    /// Assigned service bay identifier. Null until assigned.
    /// </summary>
    [JsonProperty("assignedBayId")]
    public string? AssignedBayId { get; set; }

    /// <summary>
    /// Assigned technician identifier. Null until assigned.
    /// </summary>
    [JsonProperty("assignedTechnicianId")]
    public string? AssignedTechnicianId { get; set; }

    /// <summary>
    /// Skills required for this service request (e.g., "electrical", "plumbing").
    /// </summary>
    [JsonProperty("requiredSkills")]
    public List<string> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Service priority level.
    /// </summary>
    [JsonProperty("priority")]
    public string Priority { get; set; } = default!;
}

// ---------------------------------------------------------------------------
// Embedded: CustomerSnapshotEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Point-in-time snapshot of customer info embedded within a ServiceRequest.
/// </summary>
public class CustomerSnapshotEmbedded
{
    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// True if this customer had prior service requests at this dealership.
    /// </summary>
    [JsonProperty("isReturningCustomer")]
    public bool IsReturningCustomer { get; set; }

    /// <summary>
    /// Prior request count at this dealership (0 for first-time).
    /// </summary>
    [JsonProperty("priorRequestCount")]
    public int PriorRequestCount { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: AssetInfoEmbedded (Vehicle/RV Info)
// ---------------------------------------------------------------------------

/// <summary>
/// Vehicle/RV information embedded within a ServiceRequest.
/// </summary>
public class AssetInfoEmbedded
{
    [JsonProperty("vin")]
    public string Vin { get; set; } = string.Empty;

    [JsonProperty("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: ServiceRequestAttachmentEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// File attachment metadata embedded within a ServiceRequest.
/// </summary>
public class ServiceRequestAttachmentEmbedded
{
    [JsonProperty("attachmentId")]
    public string AttachmentId { get; init; } = Guid.NewGuid().ToString();

    [JsonProperty("blobUri")]
    public string BlobUri { get; set; } = string.Empty;

    [JsonProperty("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonProperty("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------------------
// Embedded: ServiceEventEmbedded (Section 10A)
// ---------------------------------------------------------------------------

/// <summary>
/// Structured service event data per Section 10A.
/// Fields populated progressively across phases.
/// MVP captures issueCategory and componentType only.
/// </summary>
public class ServiceEventEmbedded
{
    [JsonProperty("componentType")]
    public string? ComponentType { get; set; }

    [JsonProperty("failureMode")]
    public string? FailureMode { get; set; }

    [JsonProperty("repairAction")]
    public string? RepairAction { get; set; }

    [JsonProperty("partsUsed")]
    public List<string> PartsUsed { get; set; } = [];

    [JsonProperty("laborHours")]
    public decimal? LaborHours { get; set; }

    [JsonProperty("serviceDateUtc")]
    public DateTime? ServiceDateUtc { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: DiagnosticResponseEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// A single diagnostic response from the AI-powered intake wizard.
/// </summary>
public class DiagnosticResponseEmbedded
{
    [JsonProperty("questionText")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonProperty("selectedOptions")]
    public List<string> SelectedOptions { get; set; } = [];

    [JsonProperty("freeTextResponse")]
    public string? FreeTextResponse { get; set; }
}
