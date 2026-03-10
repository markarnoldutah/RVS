using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Provides encounter management operations for patients.
/// Encounters are embedded within the Patient aggregate and include coverage decisions.
/// </summary>
[ApiController]
[Route("api/practices/{practiceId}/patients/{patientId}/encounters")]
[Authorize]
public class EncountersController : ControllerBase
{
    private readonly IEncounterService _encounterService;
    private readonly ClaimsService _claimsService;

    public EncountersController(
        IEncounterService encounterService,
        ClaimsService claimsService)
    {
        _encounterService = encounterService;
        _claimsService = claimsService;
    }

    // =====================================================
    // Encounter Operations
    // =====================================================

    [HttpPost("search")]
    public async Task<ActionResult<List<EncounterSummaryResponseDto>>> SearchPatientEncounters(
        string practiceId,
        string patientId,
        [FromBody] PatientEncounterSearchRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var encounters = await _encounterService.GetPatientEncountersAsync(
            tenantId,
            practiceId,
            patientId,
            request);

        var result = encounters.Select(e => e.ToSummaryDto(patientId, practiceId)).ToList();
        return Ok(result);
    }

    [HttpGet("{encounterId}")]
    public async Task<ActionResult<EncounterDetailResponseDto>> GetEncounter(
        string practiceId,
        string patientId,
        string encounterId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var encounter = await _encounterService.GetEncounterAsync(tenantId, practiceId, patientId, encounterId);
        var dto = encounter.ToDetailDto(patientId, practiceId);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<EncounterDetailResponseDto>> CreateEncounter(
        string practiceId,
        string patientId,
        [FromBody] EncounterCreateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var encounter = await _encounterService.CreateEncounterAsync(tenantId, practiceId, patientId, request);
        var dto = encounter.ToDetailDto(patientId, practiceId);

        return CreatedAtAction(
            nameof(GetEncounter),
            new { encounterId = dto.EncounterId, patientId, practiceId },
            dto);
    }

    [HttpPut("{encounterId}")]
    public async Task<ActionResult<EncounterDetailResponseDto>> UpdateEncounter(
        string practiceId,
        string patientId,
        string encounterId,
        [FromBody] EncounterUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var encounter = await _encounterService.UpdateEncounterAsync(tenantId, practiceId, patientId, encounterId, request);
        var dto = encounter.ToDetailDto(patientId, practiceId);

        return Ok(dto);
    }

    // =====================================================
    // Coverage Decision Operations
    // =====================================================

    [HttpGet("{encounterId}/coverage-decision")]
    public async Task<ActionResult<CoverageDecisionResponseDto>> GetCoverageDecision(
        string practiceId,
        string patientId,
        string encounterId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var decision = await _encounterService.GetCoverageDecisionAsync(tenantId, practiceId, patientId, encounterId);
        if (decision is null) return NotFound();

        return Ok(decision.ToDto());
    }

    [HttpPut("{encounterId}/coverage-decision")]
    public async Task<ActionResult<CoverageDecisionResponseDto>> SetCoverageDecision(
        string practiceId,
        string patientId,
        string encounterId,
        [FromBody] CoverageDecisionUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var decision = await _encounterService.SetCoverageDecisionAsync(tenantId, practiceId, patientId, encounterId, request);
        return Ok(decision.ToDto());
    }
}
