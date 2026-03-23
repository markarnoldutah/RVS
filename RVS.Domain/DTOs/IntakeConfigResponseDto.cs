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
}
