using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.Blazor.Manager.State;

/// <summary>
/// In-memory state backing the Manager walk-in dialog. Implements <see cref="IIntakeWizardState"/>
/// so the shared intake step components (Step 2–5) render against it unmodified.
///
/// Unlike the Intake WASM's <c>IntakeWizardState</c>, this state does not persist to
/// <c>sessionStorage</c> — a walk-in is a single-sitting entry at the service counter and
/// persistence across reloads would be confusing. Lifetime is the dialog instance.
/// </summary>
public sealed class WalkInIntakeState : IIntakeWizardState
{
    /// <summary>
    /// Location slug. Not used for routing (Manager's AI client is dealership-scoped),
    /// but carried on the interface and set from the resolved config so shared components
    /// that read it don't misbehave.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Location configuration loaded by the hosting dialog.</summary>
    public IntakeConfigResponseDto? Config { get; set; }

    // ----- Customer (Step 2) -----
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool SmsOptOut { get; set; }
    public bool EmailOptOut { get; set; }

    /// <summary>Always false — walk-ins don't arrive with a magic-link prefill.</summary>
    public bool IsPrefilled => false;

    // ----- Vehicle (Steps 3 & 4) -----
    public List<AssetInfoDto> KnownAssets { get; set; } = [];
    public string Vin { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public bool VinLookupSucceeded { get; set; }

    // ----- Issue (Step 5) -----
    public string IssueCategory { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;
    public string? Urgency { get; set; }
    public string? RvUsage { get; set; }
    public string? HasExtendedWarranty { get; set; }
    public string? ApproxPurchaseDate { get; set; }
    public bool IsCategorySuggestedByAi { get; set; }
    public bool IsUrgencySuggestedByAi { get; set; }
    public bool IsRvUsageSuggestedByAi { get; set; }

    /// <summary>Event raised when state changes to notify UI components (no persistence).</summary>
    public event Action? OnChange;

    /// <inheritdoc />
    public Task NotifyAndPersistAsync()
    {
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the service request creation DTO from the current state. Trims strings,
    /// uppercases the VIN, and collapses empty optional fields to <c>null</c>.
    /// Mirrors the anonymous intake flow's builder so the server sees the same payload shape.
    /// </summary>
    public ServiceRequestCreateRequestDto BuildCreateRequest()
    {
        return new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto
            {
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                Email = Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim()
            },
            Asset = new AssetInfoDto
            {
                AssetId = Vin.Trim().ToUpperInvariant(),
                Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer.Trim(),
                Model = string.IsNullOrWhiteSpace(Model) ? null : Model.Trim(),
                Year = Year
            },
            IssueCategory = IssueCategory.Trim(),
            IssueDescription = IssueDescription.Trim(),
            Urgency = string.IsNullOrWhiteSpace(Urgency) ? null : Urgency.Trim(),
            RvUsage = string.IsNullOrWhiteSpace(RvUsage) ? null : RvUsage.Trim(),
            SmsOptOut = SmsOptOut,
            EmailOptOut = EmailOptOut,
            HasExtendedWarranty = string.IsNullOrWhiteSpace(HasExtendedWarranty) ? null : HasExtendedWarranty.Trim(),
            ApproxPurchaseDate = string.IsNullOrWhiteSpace(ApproxPurchaseDate) ? null : ApproxPurchaseDate.Trim()
        };
    }
}
