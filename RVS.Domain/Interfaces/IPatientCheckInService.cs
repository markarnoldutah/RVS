using System.Threading;
using System.Threading.Tasks;
using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for single-screen patient check-in workflow.
/// Orchestrates patient upsert, coverage enrollment upsert, encounter creation,
/// coverage decision, and eligibility check execution in a single optimized operation.
/// 
/// RU Optimization:
/// - Standard flow (multiple APIs): ~15-20 RU (search + multiple reads/writes)
/// - Optimized check-in: ~4-6 RU (1 read + 2-3 writes)
/// </summary>
public interface IPatientCheckInService
{
    /// <summary>
    /// Performs a complete patient check-in operation.
    /// This is the primary entry point for the single-screen check-in workflow.
    /// </summary>
    /// <param name="tenantId">The tenant identifier (from JWT claim).</param>
    /// <param name="practiceId">The practice identifier (from route).</param>
    /// <param name="request">The check-in request containing patient, coverage, encounter, and eligibility data.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The complete check-in result including patient, encounter, and eligibility check results.</returns>
    Task<PatientCheckInResponseDto> CheckInAsync(
        string tenantId,
        string practiceId,
        PatientCheckInRequestDto request,
        CancellationToken cancellationToken = default);
}
