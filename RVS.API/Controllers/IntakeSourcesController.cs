using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Reports intake by distribution channel for one location (<c>Spec A-13</c>, issue #599).
///
/// Per location because that is already the product's own boundary — the intake slug, the
/// packet configuration, the recipient list are all per location, and so is the sticker on the
/// counter and the snippet on an advisor's phone.
/// </summary>
[ApiController]
[Route("api/locations/{locationId}/intake-sources")]
[Authorize]
public class IntakeSourcesController : ControllerBase
{
    private readonly IIntakeSourceReportService _service;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeSourcesController"/>.
    /// </summary>
    public IntakeSourcesController(IIntakeSourceReportService service, ClaimsService claimsService)
    {
        _service = service;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Returns submissions and redirect hits per channel for the location, with a conversion
    /// rate where one can be computed.
    ///
    /// Submissions are the number to act on. Redirect hits are reported only as the conversion
    /// denominator: messaging clients fetch a link to build a preview before anyone taps it, so
    /// raw hits over-count opens and must never be shown to a customer as such.
    /// </summary>
    /// <param name="locationId">Location identifier (route segment).</param>
    /// <param name="from">Inclusive start of the reporting window (UTC). Omit for all time.</param>
    /// <param name="to">Exclusive end of the reporting window (UTC). Omit for open-ended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// GET /api/locations/loc_hurricane/intake-sources?from=2026-09-01&amp;to=2026-10-01
    /// </example>
    [HttpGet]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<ActionResult<IntakeSourceReportResponseDto>> GetReport(
        string locationId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var report = await _service.GetForLocationAsync(tenantId, locationId, from, to, ct);

        return Ok(report);
    }
}
