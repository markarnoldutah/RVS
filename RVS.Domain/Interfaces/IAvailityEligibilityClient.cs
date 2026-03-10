using System.Threading;
using System.Threading.Tasks;
using RVS.Domain.Integrations.Availity;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Low-level client for Availity eligibility (Coverages API).
/// Treat as infrastructure boundary: no persistence, no domain mapping.
/// 
/// Async Polling Pattern:
/// 1. InitiateCoverageCheckAsync - POST /v1/coverages (returns coverage ID + initial status)
/// 2. PollCoverageStatusAsync - GET /v1/coverages/{id} (poll until terminal state)
/// </summary>
public interface IAvailityEligibilityClient
{
    /// <summary>
    /// Initiates a coverage eligibility check with Availity.
    /// POST /availity/v1/coverages (x-www-form-urlencoded 270 request)
    /// Returns immediately with Availity's coverage ID and initial status (typically "In Progress").
    /// </summary>
    Task<AvailityInitiateResponse> InitiateCoverageCheckAsync(
        AvailityEligibilityRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Polls for coverage result from Availity.
    /// GET /availity/v1/coverages/{availityCoverageId}
    /// Returns current status; caller should continue polling if status is InProgress/Retrying.
    /// </summary>
    Task<AvailityPollResponse> PollCoverageStatusAsync(
        string availityCoverageId,
        CancellationToken cancellationToken);
}
