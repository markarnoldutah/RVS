using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Features.CheckIn;

/// <summary>
/// Central state hub for the check-in workflow.
/// Following the "Blazor In Action" pattern, this class serves as a hub to access 
/// all feature-specific state containers. Each state container manages its own 
/// private state and exposes changes through defined methods only.
/// 
/// Register as a scoped service: builder.Services.AddScoped&lt;CheckInAppState&gt;();
/// </summary>
public class CheckInAppState
{
    /// <summary>
    /// State for patient discovery (search, selection, demographics).
    /// </summary>
    public PatientDiscoverySectionState PatientDiscovery { get; }

    /// <summary>
    /// State for coverage enrollments.
    /// </summary>
    public CoverageEnrollmentsSectionState CoverageEnrollments { get; }

    /// <summary>
    /// State for encounter details.
    /// </summary>
    public EncounterDetailsSectionState EncounterDetails { get; }

    /// <summary>
    /// State for check-in results and eligibility verification.
    /// </summary>
    public CheckInResultsSectionState CheckInResults { get; }

    /// <summary>
    /// State for coverage decision (COB) form.
    /// </summary>
    public CoverageDecisionSectionState CoverageDecision { get; }

    // UI State (shared across workflow)
    private bool _isSubmitting;
    private string? _errorMessage;

    /// <summary>
    /// Event raised when any state changes. Components should subscribe to this
    /// and call StateHasChanged() when notified.
    /// </summary>
    public event Action? OnStateChanged;

    public CheckInAppState()
    {
        PatientDiscovery = new PatientDiscoverySectionState();
        CoverageEnrollments = new CoverageEnrollmentsSectionState();
        EncounterDetails = new EncounterDetailsSectionState();
        CheckInResults = new CheckInResultsSectionState();
        CoverageDecision = new CoverageDecisionSectionState();
    }

    // Convenience properties delegating to CheckInResults
    public PatientCheckInResponseDto? CheckInResult => CheckInResults.CheckInResult;
    public IReadOnlyList<EligibilityCheckSummaryResponseDto> EligibilityChecks => CheckInResults.EligibilityChecks;
    public IReadOnlyList<string> Warnings => CheckInResults.Warnings;

    // UI State properties
    public bool IsSubmitting => _isSubmitting;
    public string? ErrorMessage => _errorMessage;

    // Convenience properties for Phase 1
    public bool HasSelectedPatient => PatientDiscovery.HasSelectedPatient;
    public string? SelectedPatientId => PatientDiscovery.SelectedPatientId;
    public PatientCheckInDemographicsDto Demographics => PatientDiscovery.Demographics;
    public EncounterCheckInDto Encounter => EncounterDetails.Encounter;
    public PatientDiscoveryState DiscoveryState => PatientDiscovery.DiscoveryState;

    // State mutation methods

    /// <summary>
    /// Sets the check-in result. Delegates to CheckInResults state.
    /// </summary>
    public void SetCheckInResult(PatientCheckInResponseDto result)
    {
        CheckInResults.SetCheckInResult(result);
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the eligibility checks. Delegates to CheckInResults state.
    /// </summary>
    public void SetEligibilityChecks(List<EligibilityCheckSummaryResponseDto> checks)
    {
        CheckInResults.SetEligibilityChecks(checks);
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the polling status. Delegates to CheckInResults state.
    /// </summary>
    public void SetIsPolling(bool isPolling)
    {
        CheckInResults.SetIsPolling(isPolling);
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the submitting state.
    /// </summary>
    public void SetIsSubmitting(bool isSubmitting)
    {
        _isSubmitting = isSubmitting;
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the error message.
    /// </summary>
    public void SetErrorMessage(string? message)
    {
        _errorMessage = message;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears the error message.
    /// </summary>
    public void ClearError()
    {
        _errorMessage = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Populates state from a selected patient.
    /// </summary>
    public void PopulateFromPatient(PatientDetailResponseDto patient)
    {
        PatientDiscovery.PopulateFromPatient(patient);
        CoverageEnrollments.PopulateFromPatient(patient);
        NotifyStateChanged();
    }

    /// <summary>
    /// Builds the request DTO from current state.
    /// Eligibility checks are run for coverages referenced in the coverage decision.
    /// If no coverage decision is provided, all valid coverages are checked (legacy behavior).
    /// </summary>
    public PatientCheckInRequestDto BuildRequest()
    {
        var coverages = CoverageEnrollments.CoverageEnrollments;

        // Build coverage decision if primary coverage is selected
        CoverageDecisionCheckInDto? coverageDecision = null;
        if (!string.IsNullOrEmpty(CoverageDecision.PrimaryCoverageEnrollmentId) && 
            !string.IsNullOrEmpty(CoverageDecision.CobReasonCode))
        {
            coverageDecision = new CoverageDecisionCheckInDto
            {
                PrimaryCoverageEnrollmentId = CoverageDecision.PrimaryCoverageEnrollmentId,
                SecondaryCoverageEnrollmentId = string.IsNullOrEmpty(CoverageDecision.SecondaryCoverageEnrollmentId) 
                    ? null 
                    : CoverageDecision.SecondaryCoverageEnrollmentId,
                CobReason = CoverageDecision.CobReasonCode,
                OverriddenByUser = CoverageDecision.OverrideRecommendation,
                OverrideNote = CoverageDecision.OverrideRecommendation ? CoverageDecision.OverrideNote : null
            };
        }

        return new PatientCheckInRequestDto
        {
            PatientId = PatientDiscovery.SelectedPatientId,
            Patient = PatientDiscovery.Demographics,
            CoverageEnrollments = coverages.ToList(),
            Encounter = EncounterDetails.Encounter,
            CoverageDecision = coverageDecision
        };
    }

    /// <summary>
    /// Resets the entire state for a new check-in.
    /// </summary>
    public void Reset()
    {
        PatientDiscovery.Reset();
        CoverageEnrollments.Reset();
        EncounterDetails.Reset();
        CheckInResults.Reset();
        CoverageDecision.Reset();

        _isSubmitting = false;
        _errorMessage = null;

        NotifyStateChanged();
    }

    /// <summary>
    /// Notifies subscribers that state has changed.
    /// </summary>
    public void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
