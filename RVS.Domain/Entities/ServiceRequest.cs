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
    /// Whether the customer has an extended warranty — "Yes", "No", or "Not Sure".
    /// </summary>
    [JsonProperty("hasExtendedWarranty")]
    public string? HasExtendedWarranty { get; set; }

    /// <summary>
    /// Approximate purchase date of the RV, entered as free text (e.g., "March 2023", "2022").
    /// </summary>
    [JsonProperty("approxPurchaseDate")]
    public string? ApproxPurchaseDate { get; set; }

    /// <summary>
    /// Board display order within a status column. Lower values appear first.
    /// Defaults to 0; updated when cards are reordered on the Service Board.
    /// </summary>
    [JsonProperty("boardSequence")]
    public int BoardSequence { get; set; }

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

    /// <summary>
    /// Tracks asynchronous service-packet generation for this request (<c>Spec B-1</c>, issue #434).
    /// Generation is enqueued on intake submission and re-runnable on demand; a failure here never
    /// rolls back the request. The manager app surfaces <see cref="PacketGenerationEmbedded.Status"/>
    /// and a failure that has exhausted its retries.
    /// </summary>
    [JsonProperty("packetGeneration")]
    public PacketGenerationEmbedded PacketGeneration { get; set; } = new();

    /// <summary>
    /// Tracks delivery of the generated packet by email to the location's service department
    /// (<c>Spec B-4</c>, issue #438). Delivery is idempotent per
    /// <c>(serviceRequestId, packetVersion)</c> and retried with exponential backoff; after
    /// <see cref="PacketEmailDeliveryEmbedded.MaxAttempts"/> failed attempts an alert is logged
    /// once. A delivery failure never affects packet generation, which has already succeeded.
    /// </summary>
    [JsonProperty("packetEmailDelivery")]
    public PacketEmailDeliveryEmbedded PacketEmailDelivery { get; set; } = new();

    /// <summary>
    /// The current manager-authored note shown to the customer on the status page
    /// (<c>Spec C-9</c>). <c>null</c> when no note is set — the default, and the state after a
    /// note is cleared. Exactly one note per request; editing overwrites it (no thread, no
    /// customer-visible history). One-directional: the customer can never write here.
    /// The text is never written to application logs (same rule as <see cref="IssueDescription"/>).
    /// </summary>
    [JsonProperty("customerStatusNote")]
    public CustomerStatusNoteEmbedded? CustomerStatusNote { get; set; }

    /// <summary>
    /// Sets or clears the customer-facing status note (<c>Spec C-9</c>). A null, empty, or
    /// whitespace-only <paramref name="note"/> clears it; otherwise the trimmed text is stored
    /// with an audit stamp. Either way the entity is marked updated by <paramref name="userId"/>.
    /// Callers are responsible for validating the note first
    /// (<see cref="Validation.CustomerStatusNoteValidator"/>).
    /// </summary>
    /// <param name="note">The note text, or null/blank to clear.</param>
    /// <param name="userId">The manager making the change (audit identity).</param>
    public void SetCustomerStatusNote(string? note, string? userId)
    {
        var trimmed = note?.Trim();

        CustomerStatusNote = string.IsNullOrEmpty(trimmed)
            ? null
            : new CustomerStatusNoteEmbedded
            {
                Text = trimmed,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = userId
            };

        MarkAsUpdated(userId);
    }
}

// ---------------------------------------------------------------------------
// Embedded: CustomerStatusNoteEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// The single manager-authored note that renders on the customer status page (<c>Spec C-9</c>),
/// embedded on a <see cref="ServiceRequest"/>. One-directional (manager → customer); the customer
/// has no path to write or reply. Overwritten on edit — there is no history. The text is never
/// written to application logs.
/// </summary>
public class CustomerStatusNoteEmbedded
{
    /// <summary>The note text, trimmed and sanitised on write. Shown verbatim to the customer.</summary>
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>UTC time the note was last set or edited.</summary>
    [JsonProperty("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>The manager who last set or edited the note. Null only for legacy/system writes.</summary>
    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: PacketEmailDeliveryEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// State machine for idempotent, retried delivery of the generated service packet by email
/// (<c>Spec B-4</c>, issue #438), embedded on a <see cref="ServiceRequest"/>.
///
/// The send runs from <c>PacketGenerationService</c> straight after a successful generation.
/// This block records the current delivery run — whether it is pending, delivered, or failed;
/// how many attempts it has made; the last error (never customer issue text, per <c>Spec X-7</c>);
/// and the <see cref="DeliveredPacketVersion"/> that was last delivered. Delivery is skipped when
/// <see cref="IsDeliveredFor"/> already reports the current packet version as delivered, so a
/// repeat run never double-sends. After <see cref="MaxAttempts"/> failed attempts an alert is
/// logged once (<see cref="AlertRaised"/>).
/// </summary>
public class PacketEmailDeliveryEmbedded
{
    /// <summary>Maximum delivery attempts before an alert is raised (<c>Spec B-4</c>).</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Current delivery state: <c>Pending</c> (not yet attempted for the current run),
    /// <c>Delivered</c>, or <c>Failed</c> (all attempts for the current run exhausted).
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = "Pending";

    /// <summary>Number of send attempts made in the current run.</summary>
    [JsonProperty("attemptCount")]
    public int AttemptCount { get; set; }

    /// <summary>UTC time the most recent attempt started. Null before the first attempt.</summary>
    [JsonProperty("lastAttemptAtUtc")]
    public DateTime? LastAttemptAtUtc { get; set; }

    /// <summary>
    /// The <see cref="PacketGenerationEmbedded.PacketVersion"/> that was last delivered
    /// successfully. <c>0</c> until the first successful delivery. With the service request id
    /// this is the idempotency key for delivery (<c>Spec B-4</c>).
    /// </summary>
    [JsonProperty("deliveredPacketVersion")]
    public int DeliveredPacketVersion { get; set; }

    /// <summary>UTC time of the most recent successful delivery. Null until the first success.</summary>
    [JsonProperty("deliveredAtUtc")]
    public DateTime? DeliveredAtUtc { get; set; }

    /// <summary>
    /// Short exception type and message from the most recent failed attempt, truncated. Never
    /// contains customer issue text (<c>Spec X-7</c>). Null when the last run delivered.
    /// </summary>
    [JsonProperty("lastError")]
    public string? LastError { get; set; }

    /// <summary>
    /// True once the exhausted-retries alert has been logged for the current failed run, so it is
    /// logged only once. Cleared by <see cref="BeginRun"/>.
    /// </summary>
    [JsonProperty("alertRaised")]
    public bool AlertRaised { get; set; }

    /// <summary>
    /// True when the current status is <c>Delivered</c> and the delivered version matches
    /// <paramref name="packetVersion"/> — i.e. this exact packet has already been emailed and a
    /// repeat send must be skipped.
    /// </summary>
    public bool IsDeliveredFor(int packetVersion) =>
        Status == "Delivered" && DeliveredPacketVersion == packetVersion;

    /// <summary>
    /// Starts a fresh delivery run: clears the attempt counter, error, and alert flag and returns
    /// the status to <c>Pending</c>. A prior <see cref="DeliveredPacketVersion"/> and
    /// <see cref="DeliveredAtUtc"/> are retained.
    /// </summary>
    public void BeginRun()
    {
        Status = "Pending";
        AttemptCount = 0;
        LastError = null;
        AlertRaised = false;
    }

    /// <summary>Begins a new attempt: increments the attempt count and stamps the time.</summary>
    public void MarkAttempt()
    {
        AttemptCount++;
        LastAttemptAtUtc = DateTime.UtcNow;
    }

    /// <summary>Records a successful delivery: stores the delivered version and time, clears the error.</summary>
    public void MarkDelivered(int packetVersion, DateTime deliveredAtUtc)
    {
        Status = "Delivered";
        DeliveredPacketVersion = packetVersion;
        DeliveredAtUtc = deliveredAtUtc;
        LastError = null;
    }

    /// <summary>Records a failed run: marks <c>Failed</c> and stores the sanitized error.</summary>
    public void MarkFailed(string error)
    {
        Status = "Failed";
        LastError = error;
    }

    /// <summary>Marks that the exhausted-retries alert has been logged for this failed run.</summary>
    public void MarkAlertRaised() => AlertRaised = true;
}

// ---------------------------------------------------------------------------
// Embedded: PacketGenerationEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// State machine for asynchronous service-packet generation, embedded on a
/// <see cref="ServiceRequest"/> (<c>Spec B-1</c>, issue #434).
///
/// The packet is composed and rendered off the intake request thread. This block records
/// where that work is: whether it is pending, in flight, done, or failed; how many attempts
/// have been made; the last error (never customer issue text, per <c>Spec X-7</c>); and the
/// blob path and version of the most recent successful PDF. After
/// <see cref="MaxAttempts"/> failed attempts an alert is raised once
/// (<see cref="AlertRaised"/>) and the state stays <c>Failed</c> for the manager app to show.
/// </summary>
public class PacketGenerationEmbedded
{
    /// <summary>Maximum generation attempts before an alert is raised (<c>Spec B-1</c>).</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Current generation state: <c>Pending</c> (enqueued, not started), <c>Generating</c>
    /// (an attempt is in flight), <c>Succeeded</c>, or <c>Failed</c>.
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = "Pending";

    /// <summary>Number of generation attempts made so far.</summary>
    [JsonProperty("attemptCount")]
    public int AttemptCount { get; set; }

    /// <summary>UTC time the most recent attempt started. Null before the first attempt.</summary>
    [JsonProperty("lastAttemptAtUtc")]
    public DateTime? LastAttemptAtUtc { get; set; }

    /// <summary>
    /// Short exception type and message from the most recent failure, truncated. Never contains
    /// customer issue text (<c>Spec X-7</c>). Null when the last attempt succeeded.
    /// </summary>
    [JsonProperty("lastError")]
    public string? LastError { get; set; }

    /// <summary>UTC time of the most recent successful generation. Null until the first success.</summary>
    [JsonProperty("generatedAtUtc")]
    public DateTime? GeneratedAtUtc { get; set; }

    /// <summary>
    /// Monotonic version of the generated packet, incremented on each success. <c>0</c> until the
    /// first successful generation. Used to key idempotent delivery (<c>Spec B-4</c>).
    /// </summary>
    [JsonProperty("packetVersion")]
    public int PacketVersion { get; set; }

    /// <summary>
    /// Blob name (within the attachments container) of the most recent successful packet PDF.
    /// Null until the first success.
    /// </summary>
    [JsonProperty("pdfBlobPath")]
    public string? PdfBlobPath { get; set; }

    /// <summary>
    /// True once the exhausted-retries alert has been raised for the current failure run, so it
    /// is raised only once. Cleared by <see cref="ResetForRegeneration"/>.
    /// </summary>
    [JsonProperty("alertRaised")]
    public bool AlertRaised { get; set; }

    /// <summary>
    /// How many attachments the intake client said it was about to upload (issue #516).
    /// The customer's photos are uploaded <b>after</b> the submission that creates this request,
    /// so generation started the instant the job is enqueued would render a packet with no
    /// photos. Generation holds off while <see cref="ServiceRequest.Attachments"/> is short of
    /// this number and the upload window is still open, then renders whatever arrived.
    /// <c>0</c> for requests with no attachments and for every non-intake origin — those
    /// generate immediately.
    /// </summary>
    [JsonProperty("expectedAttachmentCount")]
    public int ExpectedAttachmentCount { get; set; }

    /// <summary>Begins a new attempt: marks <c>Generating</c>, increments the attempt count, stamps the time.</summary>
    public void MarkGenerating()
    {
        Status = "Generating";
        AttemptCount++;
        LastAttemptAtUtc = DateTime.UtcNow;
    }

    /// <summary>Records a successful generation: bumps <see cref="PacketVersion"/>, stores the PDF path, clears the error.</summary>
    public void MarkSucceeded(string pdfBlobPath, DateTime generatedAtUtc)
    {
        Status = "Succeeded";
        PacketVersion++;
        PdfBlobPath = pdfBlobPath;
        GeneratedAtUtc = generatedAtUtc;
        LastError = null;
    }

    /// <summary>Records a failed attempt: marks <c>Failed</c> and stores the sanitized error.</summary>
    public void MarkFailed(string error)
    {
        Status = "Failed";
        LastError = error;
    }

    /// <summary>Marks that the exhausted-retries alert has been raised for this failure run.</summary>
    public void MarkAlertRaised() => AlertRaised = true;

    /// <summary>
    /// Resets the attempt counter, error, and alert flag and returns the state to <c>Pending</c>
    /// for an on-demand regeneration. The successful <see cref="PacketVersion"/> and
    /// <see cref="PdfBlobPath"/> are retained until the next success replaces them.
    /// </summary>
    public void ResetForRegeneration()
    {
        Status = "Pending";
        AttemptCount = 0;
        LastError = null;
        AlertRaised = false;
    }
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
    /// The customer's preferred contact method at the time of intake — one of
    /// <c>Phone</c>, <c>Text</c>, or <c>Email</c>
    /// (see <see cref="Validation.PreferredContactMethod"/>). <c>null</c> for service
    /// requests created before this field was captured.
    /// </summary>
    [JsonProperty("preferredContact")]
    public string? PreferredContact { get; set; }

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
