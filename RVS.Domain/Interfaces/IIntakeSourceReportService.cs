using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Reports intake by distribution channel for one location (<c>Spec A-13</c>, issue #599),
/// joining submissions (Cosmos service requests) with redirect hits (Table Storage) so a
/// conversion rate per channel is computable from the two stores together.
/// </summary>
public interface IIntakeSourceReportService
{
    /// <summary>
    /// Builds the channel report for one location within an optional UTC window.
    /// </summary>
    /// <param name="tenantId">Tenant owning the location. Required.</param>
    /// <param name="locationId">Location to report on. Required.</param>
    /// <param name="fromUtc">Inclusive start of the window, or <c>null</c> for all time.</param>
    /// <param name="toUtc">Exclusive end of the window, or <c>null</c> for open-ended.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IntakeSourceReportResponseDto> GetForLocationAsync(
        string tenantId,
        string locationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
}
