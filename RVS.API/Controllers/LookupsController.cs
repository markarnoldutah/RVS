using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RVS.API.Controllers;

/// <summary>
/// Provides static and tenant-scoped lookup values used throughout the app.
/// </summary>
[ApiController]
[Route("api/lookups")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ILookupService _lookupService;
    private readonly ClaimsService _claimsService;

    public LookupsController(ILookupService lookupService, ClaimsService claimsService)
    {
        _lookupService = lookupService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Gets the lookup set for the current tenant and the specified category.
    /// MVP: returns global-only values; tenant overrides will be honored later.
    /// </summary>
    /// <example>
    /// GET /api/lookups/encounter-types
    /// </example>
    [HttpGet("{lookupSetId}")]
    public async Task<ActionResult<LookupSetDto>> GetLookupAsync(string lookupSetId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow(); 

        var result = await _lookupService.GetLookupSetAsync(tenantId, lookupSetId);

        return Ok(result);
    }
}
