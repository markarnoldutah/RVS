using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Provides coverage enrollment management operations for patients.
/// Coverage enrollments are embedded within the Patient aggregate.
/// </summary>
[ApiController]
[Route("api/practices/{practiceId}/patients/{patientId}/coverage-enrollments")]
[Authorize]
public class CoverageEnrollmentsController : ControllerBase
{
    private readonly ICoverageEnrollmentService _coverageEnrollmentService;
    private readonly ClaimsService _claimsService;

    public CoverageEnrollmentsController(
        ICoverageEnrollmentService coverageEnrollmentService,
        ClaimsService claimsService)
    {
        _coverageEnrollmentService = coverageEnrollmentService;
        _claimsService = claimsService;
    }

    [HttpPost]
    public async Task<ActionResult<CoverageEnrollmentResponseDto>> AddCoverageEnrollment(
        string practiceId,
        string patientId,
        [FromBody] CoverageEnrollmentCreateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var coverage = await _coverageEnrollmentService.AddCoverageEnrollmentAsync(
            tenantId,
            practiceId,
            patientId,
            request);

        var dto = coverage.ToDto();

        return CreatedAtAction(
            actionName: "GetPatient",
            controllerName: "Patients",
            routeValues: new { patientId, practiceId },
            value: dto);
    }

    [HttpPut("{coverageEnrollmentId}")]
    public async Task<ActionResult<CoverageEnrollmentResponseDto>> UpdateCoverageEnrollment(
        string practiceId,
        string patientId,
        string coverageEnrollmentId,
        [FromBody] CoverageEnrollmentUpdateRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var coverage = await _coverageEnrollmentService.UpdateCoverageEnrollmentAsync(
            tenantId,
            practiceId,
            patientId,
            coverageEnrollmentId,
            request);

        var dto = coverage.ToDto();
        return Ok(dto);
    }

    [HttpDelete("{coverageEnrollmentId}")]
    public async Task<IActionResult> DeleteCoverageEnrollment(
        string practiceId,
        string patientId,
        string coverageEnrollmentId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        await _coverageEnrollmentService.DeleteCoverageEnrollmentAsync(
            tenantId,
            practiceId,
            patientId,
            coverageEnrollmentId);

        return NoContent();
    }
}
