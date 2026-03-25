using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Service for computing analytics rollups across service requests.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Returns an aggregated analytics summary for service requests within a tenant.
    /// Supports optional date-range and location filters.
    /// </summary>
    /// <param name="tenantId">Tenant partition key (required for tenant isolation).</param>
    /// <param name="from">Inclusive start of the date range filter (UTC). Null to omit.</param>
    /// <param name="to">Inclusive end of the date range filter (UTC). Null to omit.</param>
    /// <param name="locationId">Optional location filter for multi-location tenants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ServiceRequestAnalyticsResponseDto> GetServiceRequestSummaryAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        string? locationId = null,
        CancellationToken cancellationToken = default);
}
