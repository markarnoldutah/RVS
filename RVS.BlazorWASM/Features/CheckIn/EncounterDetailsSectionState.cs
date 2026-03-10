using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Features.CheckIn;

/// <summary>
/// State container for the EncounterDetailsSection component.
/// All state is private and can only be modified through defined methods.
/// </summary>
public class EncounterDetailsSectionState
{
    // Private backing field - state can only be changed via methods
    private EncounterCheckInDto _encounter = new()
    {
        LocationId = string.Empty,
        VisitType = string.Empty,
        VisitDate = DateTime.UtcNow
    };

    /// <summary>
    /// Read-only access to the encounter details.
    /// </summary>
    public EncounterCheckInDto Encounter => _encounter;

    /// <summary>
    /// Whether required encounter fields are complete.
    /// </summary>
    public bool HasRequiredFields =>
        !string.IsNullOrWhiteSpace(_encounter.LocationId) &&
        !string.IsNullOrWhiteSpace(_encounter.VisitType);

    /// <summary>
    /// Updates the entire encounter.
    /// </summary>
    public void UpdateEncounter(EncounterCheckInDto encounter)
    {
        _encounter = encounter;
    }

    /// <summary>
    /// Sets the location ID.
    /// </summary>
    public void SetLocationId(string locationId)
    {
        _encounter = _encounter with { LocationId = locationId };
    }

    /// <summary>
    /// Sets the visit type.
    /// </summary>
    public void SetVisitType(string visitType)
    {
        _encounter = _encounter with { VisitType = visitType };
    }

    /// <summary>
    /// Sets the visit date.
    /// </summary>
    public void SetVisitDate(DateTime? visitDate)
    {
        _encounter = _encounter with { VisitDate = visitDate };
    }

    /// <summary>
    /// Sets the external reference.
    /// </summary>
    public void SetExternalRef(string? externalRef)
    {
        _encounter = _encounter with { ExternalRef = externalRef };
    }

    /// <summary>
    /// Resets to default state.
    /// </summary>
    public void Reset()
    {
        _encounter = new()
        {
            LocationId = string.Empty,
            VisitType = string.Empty,
            VisitDate = DateTime.UtcNow
        };
    }
}
