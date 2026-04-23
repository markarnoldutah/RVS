using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Contract for the state container consumed by the shared intake step components
/// (contact, VIN, vehicle details, issue description).
///
/// Exposes only the surface those steps read or mutate — wizard-level navigation,
/// persistence, validation, and request-building stay on the concrete implementation.
/// This lets the same step components be hosted by the anonymous Intake wizard and
/// by the authenticated Manager walk-in dialog with different state backings.
/// </summary>
public interface IIntakeWizardState
{
    // ----- Location / config (read-only from the step's perspective) -----

    /// <summary>Location slug. Used by AI client calls keyed on the anonymous intake path.</summary>
    string Slug { get; }

    /// <summary>Location configuration (categories, known assets, etc.).</summary>
    IntakeConfigResponseDto? Config { get; }

    // ----- Customer (Step 2) -----

    string FirstName { get; set; }
    string LastName { get; set; }
    string Email { get; set; }
    string? Phone { get; set; }
    bool SmsOptOut { get; set; }
    bool EmailOptOut { get; set; }

    /// <summary>True when the customer info was prefilled (e.g. from magic-link token).</summary>
    bool IsPrefilled { get; }

    // ----- Vehicle (Steps 3 & 4) -----

    /// <summary>Assets known for a returning customer, enabling one-tap VIN selection.</summary>
    List<AssetInfoDto> KnownAssets { get; }

    string Vin { get; set; }
    string? Manufacturer { get; set; }
    string? Model { get; set; }
    int? Year { get; set; }

    /// <summary>True once the VIN decode API call has returned successfully.</summary>
    bool VinLookupSucceeded { get; set; }

    // ----- Issue (Step 5) -----

    string IssueCategory { get; set; }
    string IssueDescription { get; set; }
    string? Urgency { get; set; }
    string? RvUsage { get; set; }
    string? HasExtendedWarranty { get; set; }
    string? ApproxPurchaseDate { get; set; }

    /// <summary>True when the current <see cref="IssueCategory"/> was suggested by AI.</summary>
    bool IsCategorySuggestedByAi { get; set; }

    /// <summary>True when the current <see cref="Urgency"/> was suggested by AI.</summary>
    bool IsUrgencySuggestedByAi { get; set; }

    /// <summary>True when the current <see cref="RvUsage"/> was suggested by AI.</summary>
    bool IsRvUsageSuggestedByAi { get; set; }

    // ----- Change propagation -----

    /// <summary>Raises a change notification and persists state (if the host implementation persists).</summary>
    Task NotifyAndPersistAsync();
}
