using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Manages location entities for multi-location dealerships.
/// </summary>
[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _service;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="LocationsController"/>.
    /// </summary>
    public LocationsController(ILocationService service, ClaimsService claimsService)
    {
        _service = service;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Lists all locations belonging to the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<ActionResult<IReadOnlyList<LocationSummaryResponseDto>>> List(CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entities = await _service.ListByTenantAsync(tenantId, ct);

        return Ok(entities.Select(e => e.ToSummaryDto()).ToList());
    }

    /// <summary>
    /// Gets a single location by its identifier.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<ActionResult<LocationDetailDto>> GetById(string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, id, ct);

        return Ok(entity.ToDetailDto());
    }

    /// <summary>
    /// Creates a new location.
    /// </summary>
    /// <param name="request">Location creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "CanCreateLocations")]
    public async Task<ActionResult<LocationDetailDto>> Create(
        [FromBody] LocationCreateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = request.ToEntity(tenantId, tenantId);
        var created = await _service.CreateAsync(tenantId, entity, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDetailDto());
    }

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="request">Location update request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "CanUpdateLocations")]
    public async Task<ActionResult<LocationDetailDto>> Update(
        string id, [FromBody] LocationCreateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var existing = await _service.GetByIdAsync(tenantId, id, ct);
        existing.ApplyUpdate(request, tenantId);
        var updated = await _service.UpdateAsync(tenantId, id, existing, ct);

        return Ok(updated.ToDetailDto());
    }

    /// <summary>
    /// Generates a QR code URL for the location's intake form.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}/qr-code")]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<IActionResult> GetQrCode(string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, id, ct);
        var intakeUrl = $"https://intake.rvserviceflow.com/{entity.Slug}";

        return Ok(new { intakeUrl, slug = entity.Slug });
    }
}
