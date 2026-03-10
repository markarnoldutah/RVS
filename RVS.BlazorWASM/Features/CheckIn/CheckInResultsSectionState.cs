using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Features.CheckIn;

/// <summary>
/// State container for the CheckInResultsSection component.
/// Manages the check-in result data and eligibility verification results.
/// All state is private and can only be modified through defined methods.
/// </summary>
public class CheckInResultsSectionState
{
    // Private backing fields for check-in result data
    private PatientCheckInResponseDto? _checkInResult;
    private List<EligibilityCheckSummaryResponseDto> _eligibilityChecks = [];
    private List<string> _warnings = [];
    private bool _isPolling;

    // Read-only properties for check-in result data
    public PatientCheckInResponseDto? CheckInResult => _checkInResult;
    public IReadOnlyList<EligibilityCheckSummaryResponseDto> EligibilityChecks => _eligibilityChecks;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool IsPolling => _isPolling;

    /// <summary>
    /// Whether a check-in result exists.
    /// </summary>
    public bool HasResult => _checkInResult != null;

    /// <summary>
    /// Whether there are active eligibility checks in progress.
    /// </summary>
    public bool HasActiveEligibilityChecks =>
        _eligibilityChecks.Any(c => c.Status == "InProgress" || c.Status == "Pending");

    // State mutation methods for check-in result data

    /// <summary>
    /// Sets the check-in result from the API response.
    /// </summary>
    public void SetCheckInResult(PatientCheckInResponseDto result)
    {
        _checkInResult = result;
        _warnings = result.Warnings;
    }

    /// <summary>
    /// Sets the eligibility checks (from API or polling updates).
    /// </summary>
    public void SetEligibilityChecks(List<EligibilityCheckSummaryResponseDto> checks)
    {
        _eligibilityChecks = checks;
    }

    /// <summary>
    /// Sets the polling status.
    /// </summary>
    public void SetIsPolling(bool isPolling)
    {
        _isPolling = isPolling;
    }

    /// <summary>
    /// Resets to default state.
    /// </summary>
    public void Reset()
    {
        // Reset check-in result data
        _checkInResult = null;
        _eligibilityChecks = [];
        _warnings = [];
        _isPolling = false;
    }
}
