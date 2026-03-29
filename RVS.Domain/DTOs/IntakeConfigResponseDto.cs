namespace RVS.Domain.DTOs;

/// <summary>
/// Configuration and context returned for the customer intake form.
/// </summary>
public sealed record IntakeConfigResponseDto
{
    public string LocationName { get; init; } = default!;
    public string LocationSlug { get; init; } = default!;
    public string DealershipName { get; init; } = default!;
    public List<string> AcceptedFileTypes { get; init; } = [];
    public int MaxFileSizeMb { get; init; }
    public int MaxAttachments { get; init; }
    public bool AllowAnonymousIntake { get; init; }
    public List<LookupItemDto> IssueCategories { get; init; } = [];
    public CustomerInfoDto? PrefillCustomer { get; init; }

    /// <summary>
    /// Most recently used vehicle info, prefilled when the customer has asset history via a magic-link token.
    /// </summary>
    public AssetInfoDto? PrefillAsset { get; init; }

    /// <summary>
    /// All known vehicles for the returning customer, enabling one-tap VIN selection.
    /// Empty for anonymous (no token) customers.
    /// </summary>
    public List<AssetInfoDto> KnownAssets { get; init; } = [];

    /// <summary>
    /// True when a magic-link token was provided but has expired.
    /// The UI should inform the customer that their link expired while still allowing anonymous intake.
    /// </summary>
    public bool TokenExpired { get; init; }
}
