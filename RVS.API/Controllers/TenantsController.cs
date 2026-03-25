using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.API.Services;

namespace RVS.API.Controllers;

/// <summary>
/// Manages tenant-wide configuration and settings.
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize]
// TODO: Add role-based authorization [Authorize(Roles = "TenantAdmin")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ClaimsService _claimsService;

    public TenantsController(ITenantService tenantService, ClaimsService claimsService)
    {
        _tenantService = tenantService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Creates the initial tenant configuration (bootstrap).
    /// </summary>
    [HttpPost("config")]
    [Authorize(Policy = "CanManageTenantConfig")]
    public async Task<ActionResult<TenantConfigResponseDto>> CreateTenantConfig([FromBody] TenantConfigCreateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var config = await _tenantService.CreateTenantConfigAsync(tenantId, request);
        var dto = config.ToDto();

        return CreatedAtAction(nameof(GetTenantConfig), dto);
    }

    /// <summary>
    /// Retrieves the current tenant configuration.
    /// </summary>
    [HttpGet("config")]
    [Authorize(Policy = "CanManageTenantConfig")]
    public async Task<ActionResult<TenantConfigResponseDto>> GetTenantConfig()
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var config = await _tenantService.GetTenantConfigAsync(tenantId);
        var dto = config.ToDto();

        return Ok(dto);
    }

    /// <summary>
    /// Updates the tenant configuration.
    /// </summary>
    [HttpPut("config")]
    [Authorize(Policy = "CanManageTenantConfig")]
    public async Task<ActionResult<TenantConfigResponseDto>> UpdateTenantConfig([FromBody] TenantConfigUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var config = await _tenantService.UpdateTenantConfigAsync(tenantId, request);
        var dto = config.ToDto();

        return Ok(dto);
    }

    /// <summary>
    /// Retrieves the tenant access gate settings (login enabled/disabled state).
    /// This endpoint allows client applications to check if logins are enabled for the tenant.
    /// </summary>
    [HttpGet("access-gate")]
    [Authorize(Policy = "CanManageTenantConfig")]
    public async Task<ActionResult<TenantAccessGateDto>> GetAccessGate()
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var gate = await _tenantService.GetAccessGateAsync(tenantId);
        
        var dto = new TenantAccessGateDto
        {
            LoginsEnabled = gate.LoginsEnabled,
            DisabledReason = gate.DisabledReason,
            DisabledMessage = gate.DisabledMessage,
            SupportContactEmail = gate.SupportContactEmail,
            DisabledAtUtc = gate.DisabledAtUtc
        };

        return Ok(dto);
    }
}
