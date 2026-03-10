using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Features.CheckIn;

/// <summary>
/// State container for the CoverageDecisionSection component.
/// Manages the coverage decision form state (COB - Coordination of Benefits).
/// All state is private and can only be modified through defined methods.
/// </summary>
public class CoverageDecisionSectionState
{
    // Private backing fields for coverage decision form
    private string? _primaryCoverageEnrollmentId;
    private string? _secondaryCoverageEnrollmentId;
    private string? _cobReasonCode;
    private bool _overrideRecommendation;
    private string? _overrideNote;
    private bool _decisionSkipped;
    private CobRecommendation? _recommendedDecision;
    private List<CoverageOption> _coverageOptions = [];

    // Read-only properties for coverage decision form
    public string? PrimaryCoverageEnrollmentId => _primaryCoverageEnrollmentId;
    public string? SecondaryCoverageEnrollmentId => _secondaryCoverageEnrollmentId;
    public string? CobReasonCode => _cobReasonCode;
    public bool OverrideRecommendation => _overrideRecommendation;
    public string? OverrideNote => _overrideNote;
    public bool DecisionSkipped => _decisionSkipped;
    public CobRecommendation? RecommendedDecision => _recommendedDecision;
    public IReadOnlyList<CoverageOption> CoverageOptions => _coverageOptions;

    /// <summary>
    /// Whether a recommendation has been calculated.
    /// </summary>
    public bool HasRecommendation => _recommendedDecision != null;

    /// <summary>
    /// Whether the decision form can be confirmed.
    /// When not overriding, just need a recommendation.
    /// When overriding, need primary coverage and COB reason to be set.
    /// </summary>
    public bool CanConfirmDecision =>
        _recommendedDecision != null &&
        (!_overrideRecommendation ||
         (!string.IsNullOrEmpty(_primaryCoverageEnrollmentId) && !string.IsNullOrEmpty(_cobReasonCode)));

    // State mutation methods for coverage decision form

    /// <summary>
    /// Sets the primary coverage enrollment ID.
    /// </summary>
    public void SetPrimaryCoverage(string? coverageEnrollmentId)
    {
        _primaryCoverageEnrollmentId = coverageEnrollmentId;
    }

    /// <summary>
    /// Sets the secondary coverage enrollment ID.
    /// </summary>
    public void SetSecondaryCoverage(string? coverageEnrollmentId)
    {
        _secondaryCoverageEnrollmentId = coverageEnrollmentId;
    }

    /// <summary>
    /// Sets the COB reason code.
    /// </summary>
    public void SetCobReasonCode(string? code)
    {
        _cobReasonCode = code;
    }

    /// <summary>
    /// Sets the override recommendation flag.
    /// </summary>
    public void SetOverrideRecommendation(bool value)
    {
        _overrideRecommendation = value;
    }

    /// <summary>
    /// Sets the override note.
    /// </summary>
    public void SetOverrideNote(string? note)
    {
        _overrideNote = note;
    }

    /// <summary>
    /// Marks the decision as skipped.
    /// </summary>
    public void SkipDecision()
    {
        _decisionSkipped = true;
    }

    /// <summary>
    /// Shows the decision form (un-skips).
    /// </summary>
    public void ShowDecisionForm()
    {
        _decisionSkipped = false;
    }

    /// <summary>
    /// Sets the coverage options.
    /// </summary>
    public void SetCoverageOptions(List<CoverageOption> options)
    {
        _coverageOptions = options;
    }

    /// <summary>
    /// Applies a recommendation for the coverage decision.
    /// </summary>
    public void ApplyRecommendation(
        string primaryCoverageId,
        string? secondaryCoverageId,
        string cobReasonCode,
        string recommendation,
        string reason)
    {
        _primaryCoverageEnrollmentId = primaryCoverageId;
        _secondaryCoverageEnrollmentId = secondaryCoverageId;
        _cobReasonCode = cobReasonCode;
        _recommendedDecision = new CobRecommendation
        {
            Recommendation = recommendation,
            Reason = reason
        };
    }

    /// <summary>
    /// Builds the coverage decision update request.
    /// </summary>
    public CoverageDecisionUpdateRequestDto BuildRequest()
    {
        return new CoverageDecisionUpdateRequestDto
        {
            PrimaryCoverageEnrollmentId = _primaryCoverageEnrollmentId!,
            SecondaryCoverageEnrollmentId = string.IsNullOrEmpty(_secondaryCoverageEnrollmentId)
                ? null
                : _secondaryCoverageEnrollmentId,
            CobReason = _cobReasonCode!,
            OverriddenByUser = _overrideRecommendation,
            OverrideNote = _overrideRecommendation ? _overrideNote : null
        };
    }

    /// <summary>
    /// Resets to default state.
    /// </summary>
    public void Reset()
    {
        _primaryCoverageEnrollmentId = null;
        _secondaryCoverageEnrollmentId = null;
        _cobReasonCode = null;
        _overrideRecommendation = false;
        _overrideNote = null;
        _decisionSkipped = false;
        _recommendedDecision = null;
        _coverageOptions = [];
    }
}

/// <summary>
/// Represents a coverage option for the decision dropdown.
/// </summary>
public class CoverageOption
{
    public string CoverageEnrollmentId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PlanType { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>
/// Represents a COB recommendation.
/// </summary>
public class CobRecommendation
{
    public string Recommendation { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
