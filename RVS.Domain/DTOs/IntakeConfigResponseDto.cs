namespace RVS.Domain.DTOs;

/// <summary>
/// Configuration and context returned for the customer intake form.
/// </summary>
public sealed record IntakeConfigResponseDto
{
    public string LocationName { get; init; } = default!;
    public string LocationSlug { get; init; } = default!;
    public string DealershipName { get; init; } = default!;

    /// <summary>
    /// Public-facing phone number for the location, surfaced so the customer-facing
    /// intake UI can include it in fallback messages (e.g. when the selected location's
    /// capabilities do not match the issue). Null when the location has no phone configured.
    /// </summary>
    public string? LocationPhone { get; init; }

    /// <summary>
    /// The location's dealer logo and header colour for the intake form's chrome
    /// (<c>Spec A-16</c>, issue #470). Empty fields fall back to the RV Intake defaults.
    /// </summary>
    public LocationBrandingDto Branding { get; init; } = new();

    /// <summary>
    /// The location's own questions, shown on the diagnostic step after the AI's and answered in
    /// free text (<c>Spec A-18</c>, issue #785). Empty when the location has added none.
    /// </summary>
    public List<string> DealerQuestions { get; init; } = [];

    public List<string> AcceptedFileTypes { get; init; } = [];
    public int MaxFileSizeMb { get; init; }
    public int MaxAttachments { get; init; }
    public bool AllowAnonymousIntake { get; init; }
    public List<LookupItemDto> IssueCategories { get; init; } = [];

    /// <summary>
    /// True when the location's tenant has been disabled for longer than the capture window, so
    /// the slug no longer accepts new requests (<c>Spec A-19</c>, issue #478). The intake app shows
    /// a neutral notice in place of the wizard. Why the tenant is disabled never reaches the wire.
    /// </summary>
    public bool IntakeExpired { get; init; }
}
