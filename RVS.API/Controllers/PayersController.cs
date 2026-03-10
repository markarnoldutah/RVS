using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.API.Services;

namespace RVS.API.Controllers;

/// <summary>
/// Manages payer retrieval, search, and metadata used for eligibility and COB.
/// </summary>
[ApiController]
[Route("api/payers")]
[Authorize]
public class PayersController : ControllerBase
{
    private readonly IPayerService _payerService;
    private readonly ClaimsService _claimsService;

    public PayersController(IPayerService payerService, ClaimsService claimsService)
    {
        _payerService = payerService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Retrieves all payers available to the tenant.
    /// Returns both GLOBAL payers (shared across all tenants) and tenant-specific payers.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PayerResponseDto>>> GetPayers()
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        
        // Service returns entities (GLOBAL + tenant-specific)
        var payers = await _payerService.SearchPayersAsync(tenantId, null, null);
        
        var dtos = payers.ToDto();
        
        return Ok(dtos);
    }

    /// <summary>
    /// Searches payers by name or plan type.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<PayerResponseDto>>> SearchPayers(
        [FromQuery] string? planType = null,
        [FromQuery] string? search = null)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        
        var payers = await _payerService.SearchPayersAsync(tenantId, planType, search);
        
        var dtos = payers.ToDto();
        
        return Ok(dtos);
    }

    /// <summary>
    /// Retrieves detailed information for a specific payer.
    /// </summary>
    [HttpGet("{payerId}")]
    public async Task<ActionResult<PayerResponseDto>> GetPayer(string payerId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var payer = await _payerService.GetPayerAsync(tenantId, payerId);
        
        var dto = payer.ToDto();
        
        return Ok(dto);
    }

    /// <summary>
    /// Retrieves all payer configurations for a specific practice.
    /// </summary>
    [HttpGet("practices/{practiceId}/config")]
    public async Task<ActionResult<List<PayerConfigDto>>> GetPayerConfigs(string practiceId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var configs = await _payerService.GetPayerConfigsAsync(tenantId, practiceId);
        var dtos = configs.ToDto();

        return Ok(dtos);
    }

    /// <summary>
    /// Updates a specific payer configuration for a practice.
    /// </summary>
    [HttpPut("practices/{practiceId}/{payerId}/config")]
    public async Task<ActionResult<PayerConfigDto>> UpdatePayerConfig(
        string practiceId,
        string payerId,
        [FromBody] PayerConfigUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var config = await _payerService.UpdatePayerConfigAsync(tenantId, practiceId, payerId, request);
        var dto = config.ToDto();

        return Ok(dto);
    }
}
