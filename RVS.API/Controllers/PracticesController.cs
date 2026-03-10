using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.API.Services;

namespace RVS.API.Controllers;

/// <summary>
/// Manages practice-level information including providers, locations, and NPI settings.
/// </summary>
[ApiController]
[Route("api/practices")]
[Authorize]
public class PracticesController : ControllerBase
{
    private readonly IPracticeService _practiceService;
    private readonly ClaimsService _claimsService;

    public PracticesController(IPracticeService practiceService, ClaimsService claimsService)
    {
        _practiceService = practiceService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Retrieves all practices for the tenant.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PracticeSummaryResponseDto>>> GetPractices(
        [FromQuery] bool includeLocations = true)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        
        // Service returns entities
        var practices = await _practiceService.GetPracticesAsync(tenantId, includeLocations);
        
        // Convert to DTOs at boundary
        var dtos = practices.ToSummaryDto();
        
        return Ok(dtos);
    }

    /// <summary>
    /// Retrieves a specific practice by ID.
    /// </summary>
    [HttpGet("{practiceId}")]
    public async Task<ActionResult<PracticeDetailResponseDto>> GetPractice(string practiceId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        // Service returns entity
        var practice = await _practiceService.GetPracticeAsync(tenantId, practiceId);
        
        // Convert to DTO at boundary
        var dto = practice.ToDetailDto();
        
        return Ok(dto);
    }
}
