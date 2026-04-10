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

    /// <summary>
    /// Customer-reported urgency from the intake form (e.g., "This week", "Today").
    /// </summary>
    [JsonProperty("urgency")]
    public string? Urgency { get; set; }

    /// <summary>
    /// Customer's RV usage pattern (e.g., "Full-time", "Weekend trips").
    /// </summary>
    [JsonProperty("rvUsage")]
    public string? RvUsage { get; set; }

    /// <summary>
    /// AI enrichment provenance metadata. Records which AI capabilities were
    /// used during intake, their providers, and confidence scores.
    /// Null when no AI enrichment was applied.
    /// </summary>
    [JsonProperty("aiEnrichment")]
    public AiEnrichmentMetadataEmbedded? AiEnrichment { get; set; }

    /// <summary>
    /// Dealer-to-customer and customer-to-dealer messages linked to this service request.
    /// Embedded in the SR document for single-read performance. Capped at 50 messages.
    /// </summary>
    [JsonProperty("messages")]
    public List<MessageEmbedded> Messages { get; set; } = [];
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
    /// <summary>
    /// Asset identifier — the 17-character Vehicle Identification Number (VIN).
    /// </summary>
    [JsonProperty("assetId")]
    public string AssetId { get; set; } = string.Empty;

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

// ---------------------------------------------------------------------------
// Embedded: AiEnrichmentMetadataEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Records AI provenance metadata for the intake workflow so that the manager app
/// can display which fields were AI-generated, by which provider, and with what confidence.
/// </summary>
public class AiEnrichmentMetadataEmbedded
{
    /// <summary>
    /// Provider that produced the issue category suggestion, or <c>null</c> if manually selected.
    /// </summary>
    [JsonProperty("categorySuggestionProvider")]
    public string? CategorySuggestionProvider { get; set; }

    /// <summary>
    /// Confidence score of the issue category suggestion (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    [JsonProperty("categorySuggestionConfidence")]
    public double? CategorySuggestionConfidence { get; set; }

    /// <summary>
    /// Provider that generated diagnostic questions (e.g. Azure OpenAI vs rule-based).
    /// </summary>
    [JsonProperty("diagnosticQuestionsProvider")]
    public string? DiagnosticQuestionsProvider { get; set; }

    /// <summary>
    /// Provider that transcribed the issue audio, or <c>null</c> if no audio was submitted.
    /// </summary>
    [JsonProperty("transcriptionProvider")]
    public string? TranscriptionProvider { get; set; }

    /// <summary>
    /// Confidence score of the audio transcription (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    [JsonProperty("transcriptionConfidence")]
    public double? TranscriptionConfidence { get; set; }

    /// <summary>
    /// Provider that extracted the VIN from a photo, or <c>null</c> if VIN was entered manually.
    /// </summary>
    [JsonProperty("vinExtractionProvider")]
    public string? VinExtractionProvider { get; set; }

    /// <summary>
    /// Confidence score of VIN extraction (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    [JsonProperty("vinExtractionConfidence")]
    public double? VinExtractionConfidence { get; set; }

    /// <summary>
    /// Provider that inferred urgency and RV usage, or <c>null</c> if not inferred.
    /// </summary>
    [JsonProperty("insightsSuggestionProvider")]
    public string? InsightsSuggestionProvider { get; set; }

    /// <summary>
    /// Confidence score of the urgency/RV-usage inference (<c>0.0</c> to <c>1.0</c>).
    /// </summary>
    [JsonProperty("insightsSuggestionConfidence")]
    public double? InsightsSuggestionConfidence { get; set; }

    /// <summary>
    /// UTC timestamp when AI enrichment metadata was last computed.
    /// </summary>
    [JsonProperty("enrichedAtUtc")]
    public DateTime? EnrichedAtUtc { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: MessageEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// A single message in the dealer ↔ customer conversation thread,
/// embedded within a <see cref="ServiceRequest"/> document.
/// </summary>
public class MessageEmbedded
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Direction of the message: "outbound" (dealer → customer) or "inbound" (customer → dealer).
    /// </summary>
    [JsonProperty("direction")]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Channel used to deliver the message: "sms" or "email".
    /// </summary>
    [JsonProperty("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Who sent the message: "dealer" or "customer".
    /// </summary>
    [JsonProperty("senderType")]
    public string SenderType { get; set; } = string.Empty;

    /// <summary>
    /// User ID of the dealer staff member who sent the message. Null for customer-sent messages.
    /// </summary>
    [JsonProperty("senderUserId")]
    public string? SenderUserId { get; set; }

    /// <summary>
    /// Display name of the sender (e.g., "Sarah (Service Advisor)"). Null for customer-sent messages.
    /// </summary>
    [JsonProperty("senderDisplayName")]
    public string? SenderDisplayName { get; set; }

    /// <summary>
    /// Phone number of the sender for inbound SMS messages.
    /// </summary>
    [JsonProperty("senderPhone")]
    public string? SenderPhone { get; set; }

    /// <summary>
    /// Phone number of the recipient for outbound SMS messages.
    /// </summary>
    [JsonProperty("recipientPhone")]
    public string? RecipientPhone { get; set; }

    /// <summary>
    /// Email address of the recipient for outbound email messages.
    /// </summary>
    [JsonProperty("recipientEmail")]
    public string? RecipientEmail { get; set; }

    /// <summary>
    /// Message body text.
    /// </summary>
    [JsonProperty("body")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was sent (outbound messages).
    /// </summary>
    [JsonProperty("sentAtUtc")]
    public DateTime? SentAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the message was received (inbound messages).
    /// </summary>
    [JsonProperty("receivedAtUtc")]
    public DateTime? ReceivedAtUtc { get; set; }

    /// <summary>
    /// Delivery status for outbound messages (e.g., "queued", "sent", "delivered", "failed").
    /// </summary>
    [JsonProperty("deliveryStatus")]
    public string? DeliveryStatus { get; set; }

    /// <summary>
    /// UTC timestamp when the delivery status was last updated.
    /// </summary>
    [JsonProperty("deliveryStatusUpdatedAtUtc")]
    public DateTime? DeliveryStatusUpdatedAtUtc { get; set; }
}
