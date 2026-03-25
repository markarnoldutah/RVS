using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Provides analytics rollups for the service manager dashboard.
/// </summary>
[ApiController]
[Route("api/dealerships/{dealershipId}/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="AnalyticsController"/>.
    /// </summary>
    public AnalyticsController(IAnalyticsService analyticsService, ClaimsService claimsService)
    {
        _analyticsService = analyticsService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Returns an aggregated analytics summary for service requests.
    /// Supports optional date-range and location filters via query parameters.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="from">Inclusive start of the date range filter (UTC).</param>
    /// <param name="to">Inclusive end of the date range filter (UTC).</param>
    /// <param name="locationId">Optional location filter for multi-location tenants.</param>
    /// <example>
    /// GET /api/dealerships/{id}/analytics/service-requests/summary?from=2025-01-01&amp;to=2025-12-31&amp;locationId=loc_slc
    /// </example>
    [HttpGet("service-requests/summary")]
    [Authorize(Policy = "CanReadAnalytics")]
    public async Task<ActionResult<ServiceRequestAnalyticsResponseDto>> GetServiceRequestSummary(
        string dealershipId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? locationId = null)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var result = await _analyticsService.GetServiceRequestSummaryAsync(tenantId, from, to, locationId);

        return Ok(result);
    }
}
