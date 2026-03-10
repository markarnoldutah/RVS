using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Features.CheckIn;

/// <summary>
/// State container for the CoverageEnrollmentsSection component.
/// All state is private and can only be modified through defined methods.
/// </summary>
public class CoverageEnrollmentsSectionState
{
    // Private backing field - state can only be changed via methods
    private List<CoverageEnrollmentUpsertDto> _coverageEnrollments = [];

    /// <summary>
    /// Read-only access to coverage enrollments.
    /// </summary>
    public IReadOnlyList<CoverageEnrollmentUpsertDto> CoverageEnrollments => _coverageEnrollments;

    /// <summary>
    /// Gets the count of coverage enrollments.
    /// </summary>
    public int Count => _coverageEnrollments.Count;

    /// <summary>
    /// Populates coverage enrollments from a loaded patient.
    /// </summary>
    public void PopulateFromPatient(PatientDetailResponseDto patient)
    {
        _coverageEnrollments = patient.CoverageEnrollments
            .Where(c => c.IsEnabled)
            .Select(c => new CoverageEnrollmentUpsertDto
            {
                CoverageEnrollmentId = c.CoverageEnrollmentId,
                PayerId = c.PayerId,
                PlanType = c.PlanType,
                MemberId = c.MemberId,
                GroupNumber = c.GroupNumber,
                RelationshipToSubscriber = c.RelationshipToSubscriber,
                SubscriberFirstName = c.SubscriberFirstName,
                SubscriberLastName = c.SubscriberLastName,
                SubscriberDob = c.SubscriberDob,
                IsEmployerPlan = c.IsEmployerPlan,
                EffectiveDate = c.EffectiveDate,
                TerminationDate = c.TerminationDate,
                CobPriorityHint = c.CobPriorityHint,
                CobNotes = c.CobNotes
            })
            .ToList();
    }

    /// <summary>
    /// Adds a new empty coverage enrollment.
    /// </summary>
    public void AddCoverage()
    {
        _coverageEnrollments.Add(new CoverageEnrollmentUpsertDto
        {
            CoverageEnrollmentId = Guid.NewGuid().ToString(),
            PayerId = string.Empty,
            PlanType = "Vision",
            MemberId = string.Empty,
            RelationshipToSubscriber = "Self"
        });
    }

    /// <summary>
    /// Updates a coverage at the specified index.
    /// </summary>
    public void UpdateCoverage(int index, CoverageEnrollmentUpsertDto coverage)
    {
        if (index >= 0 && index < _coverageEnrollments.Count)
        {
            _coverageEnrollments[index] = coverage;
        }
    }

    /// <summary>
    /// Removes a coverage at the specified index.
    /// </summary>
    public void RemoveCoverage(int index)
    {
        if (index >= 0 && index < _coverageEnrollments.Count)
        {
            _coverageEnrollments.RemoveAt(index);
        }
    }

    /// <summary>
    /// Gets a coverage at the specified index.
    /// </summary>
    public CoverageEnrollmentUpsertDto? GetCoverage(int index)
    {
        if (index >= 0 && index < _coverageEnrollments.Count)
        {
            return _coverageEnrollments[index];
        }
        return null;
    }

    /// <summary>
    /// Gets the count of coverages that have valid PayerId and MemberId for eligibility checks.
    /// </summary>
    public int EligibleForCheckCount => _coverageEnrollments.Count(c => 
        !string.IsNullOrWhiteSpace(c.PayerId) && !string.IsNullOrWhiteSpace(c.MemberId));

    /// <summary>
    /// Resets to empty state.
    /// </summary>
    public void Reset()
    {
        _coverageEnrollments = [];
    }
}
