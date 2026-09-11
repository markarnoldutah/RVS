using RVS.Domain.Entities;

namespace RVS.Domain.Integrations;

/// <summary>
/// Produces the structured preliminary assessment shown in the packet's Preliminary assessment
/// section (<c>Spec B-2</c> item 5, issue #507): a probable cause, possible fixes and likely
/// parts, grounded on the intake's category, unit, description and diagnostic Q&amp;A.
/// Implementations degrade to a rule-based result rather than throwing on provider failure.
/// </summary>
public interface IPreliminaryAssessmentService
{
    /// <summary>
    /// Assesses <paramref name="serviceRequest"/>. Returns an abstention (confidence
    /// <c>abstain</c>, no content) when there is not enough to go on.
    /// </summary>
    /// <param name="serviceRequest">The submitted service request to assess.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assessment, stamped with its provider and generation time.</returns>
    Task<PreliminaryAssessmentEmbedded> AssessAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default);
}
