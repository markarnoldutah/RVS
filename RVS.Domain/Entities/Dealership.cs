using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Dealership entity — represents an RV service dealership tenant.
///
/// Cosmos DB partition key: /tenantId
/// </summary>
public class Dealership : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "dealership";

    /// <summary>
    /// URL-safe slug for the dealership (e.g., "camping-world-salt-lake").
    /// </summary>
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// URL to the dealership's logo image.
    /// </summary>
    [JsonProperty("logoUrl")]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Service department contact email.
    /// </summary>
    [JsonProperty("serviceEmail")]
    public string? ServiceEmail { get; set; }

    /// <summary>
    /// Service department contact phone.
    /// </summary>
    [JsonProperty("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// Intake form configuration for this dealership.
    /// </summary>
    [JsonProperty("intakeConfig")]
    public IntakeFormConfigEmbedded IntakeConfig { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Embedded: IntakeFormConfigEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Configuration for the customer intake form at a dealership or location.
/// </summary>
public class IntakeFormConfigEmbedded
{
    /// <summary>
    /// Accepted file types for attachments (e.g., ".jpg", ".png", ".mp4").
    /// </summary>
    [JsonProperty("acceptedFileTypes")]
    public List<string> AcceptedFileTypes { get; set; } = [".jpg", ".jpeg", ".png", ".mp4", ".m4a", ".wav"];

    /// <summary>
    /// Maximum file size in megabytes for a single attachment. Range: 1–100.
    /// </summary>
    [JsonProperty("maxFileSizeMb")]
    public int MaxFileSizeMb { get; set; } = 25;

    /// <summary>
    /// Maximum number of attachments per service request. Range: 1–10.
    /// </summary>
    [JsonProperty("maxAttachments")]
    public int MaxAttachments { get; set; } = 10;

    /// <summary>
    /// Optional context appended to the Azure OpenAI system prompt. Max 500 characters.
    /// </summary>
    [JsonProperty("aiContext")]
    public string? AiContext { get; set; }

    /// <summary>
    /// If false, intake requires a specific tenant-issued token (Phase 2).
    /// </summary>
    [JsonProperty("allowAnonymousIntake")]
    public bool AllowAnonymousIntake { get; set; } = true;
}
