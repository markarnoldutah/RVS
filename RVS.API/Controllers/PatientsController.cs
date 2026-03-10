using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Provides patient search, retrieval, and demographic management operations scoped to a tenant + practice.
/// 
/// Related operations are handled by separate controllers:
/// - CoverageEnrollmentsController: Coverage enrollment management
/// - EncountersController: Encounter and coverage decision management
/// - EligibilityChecksController: Eligibility check operations
/// </summary>
[ApiController]
[Route("api/practices/{practiceId}/patients")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly ClaimsService _claimsService;

    public PatientsController(IPatientService patientService, ClaimsService claimsService)
    {
        _patientService = patientService;
        _claimsService = claimsService;
    }

    [HttpPost("search")]
    public async Task<ActionResult<PagedResult<PatientSearchResultResponseDto>>> SearchPatients(
        string practiceId,
        [FromBody] PatientSearchRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var patients = await _patientService.SearchPatientsAsync(tenantId, practiceId, request);
        var result = patients.ToSearchResultDto();

        return Ok(result);
    }

    [HttpGet("{patientId}")]
    public async Task<ActionResult<PatientDetailResponseDto>> GetPatient(
        string practiceId,
        string patientId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var patient = await _patientService.GetPatientAsync(tenantId, practiceId, patientId);
        var dto = patient.ToDetailDto();

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<PatientDetailResponseDto>> CreatePatient(
        string practiceId,
        [FromBody] PatientCreateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var patient = await _patientService.CreatePatientAsync(tenantId, practiceId, request);
        var dto = patient.ToDetailDto();

        return CreatedAtAction(
            nameof(GetPatient),
            new { patientId = dto.PatientId, practiceId },
            dto);
    }

    [HttpPut("{patientId}")]
    public async Task<ActionResult<PatientDetailResponseDto>> UpdatePatient(
        string practiceId,
        string patientId,
        [FromBody] PatientUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var patient = await _patientService.UpdatePatientAsync(tenantId, practiceId, patientId, request);
        var dto = patient.ToDetailDto();

        return Ok(dto);
    }
}
