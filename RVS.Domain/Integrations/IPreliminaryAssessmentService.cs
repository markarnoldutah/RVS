using RVS.Domain.Entities;

namespace RVS.Domain.Integrations;

/// <summary>
/// Produces the structured preliminary assessment shown in the packet's Preliminary assessment
/// section (<c>Spec B-2</c> item 5, issue #507): a probable cause, possible fixes and likely
/// parts, grounded on the intake's category, unit, description and diagnostic Q&amp;A — and, when
/// photos are supplied, on what can be read off them (issue #772).
/// Implementations degrade to a rule-based result rather than throwing on provider failure.
/// </summary>
public interface IPreliminaryAssessmentService
{
    /// <summary>
    /// Assesses <paramref name="serviceRequest"/>. Returns an abstention (confidence
    /// <c>abstain</c>, no content) when there is not enough to go on.
    /// </summary>
    /// <param name="serviceRequest">The submitted service request to assess.</param>
    /// <param name="photos">
    /// The request's photos, in attachment order. <c>null</c> or empty assesses the text alone,
    /// exactly as before photos were read. An implementation that cannot read images ignores them.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assessment, stamped with its provider and generation time.</returns>
    Task<PreliminaryAssessmentEmbedded> AssessAsync(
        ServiceRequest serviceRequest,
        IReadOnlyList<AssessmentPhoto>? photos = null,
        CancellationToken cancellationToken = default);
}
