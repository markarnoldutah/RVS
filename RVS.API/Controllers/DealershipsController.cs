using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Manages dealership entities for the authenticated portal.
/// </summary>
[ApiController]
[Route("api/dealerships")]
[Authorize]
public class DealershipsController : ControllerBase
{
    private readonly IDealershipService _service;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="DealershipsController"/>.
    /// </summary>
    public DealershipsController(IDealershipService service, ClaimsService claimsService)
    {
        _service = service;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Lists all dealerships belonging to the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "CanReadDealerships")]
    public async Task<ActionResult<IReadOnlyList<DealershipSummaryDto>>> List(CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entities = await _service.ListByTenantAsync(tenantId, ct);

        return Ok(entities.Select(e => e.ToSummaryDto()).ToList());
    }

    /// <summary>
    /// Gets a single dealership by its identifier.
    /// </summary>
    /// <param name="id">Dealership identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanReadDealerships")]
    public async Task<ActionResult<DealershipDetailDto>> GetById(string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, id, ct);

        return Ok(entity.ToDetailDto());
    }

    /// <summary>
    /// Updates an existing dealership.
    /// </summary>
    /// <param name="id">Dealership identifier.</param>
    /// <param name="request">The update request DTO.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "CanUpdateDealerships")]
    public async Task<ActionResult<DealershipDetailDto>> Update(
        string id, [FromBody] DealershipUpdateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var updated = await _service.UpdateAsync(tenantId, id, request, ct);

        return Ok(updated.ToDetailDto());
    }
}
