using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Provides single-screen patient check-in workflow operations.
/// Optimized for Blazor WASM and other front-end clients that need
/// to perform patient lookup/upsert, coverage enrollment, encounter creation,
/// coverage decision, and eligibility check execution in a single operation.
/// 
/// RU Optimization:
/// - Separate API calls: ~15-20 RU
/// - Single check-in call: ~4-6 RU
/// </summary>
[ApiController]
[Route("api/practices/{practiceId}/check-in")]
[Authorize]
public class PatientCheckInController : ControllerBase
{
    private readonly IPatientCheckInService _checkInService;
    private readonly ClaimsService _claimsService;

    public PatientCheckInController(
        IPatientCheckInService checkInService,
        ClaimsService claimsService)
    {
        _checkInService = checkInService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Performs a complete patient check-in operation.
    /// 
    /// This endpoint supports the full check-in workflow:
    /// 1. Patient lookup or creation (with demographics)
    /// 2. Coverage enrollment(s) creation or update
    /// 3. Encounter creation or update
    /// 4. Coverage decision (COB) assignment
    /// 5. Eligibility check(s) execution
    /// 
    /// All operations are performed in a single request with optimized
    /// Cosmos DB operations (~4-6 RU vs ~15-20 RU for separate calls).
    /// </summary>
    /// <param name="practiceId">The practice identifier from the route.</param>
    /// <param name="request">The check-in request containing all workflow data.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Complete check-in result including patient, encounter, and eligibility results.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(PatientCheckInResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PatientCheckInResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientCheckInResponseDto>> CheckIn(
        string practiceId,
        [FromBody] PatientCheckInRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var result = await _checkInService.CheckInAsync(
            tenantId,
            practiceId,
            request,
            cancellationToken);

        // Return 201 if patient was created, 200 otherwise
        if (result.Patient.WasCreated)
        {
            return CreatedAtAction(
                actionName: "GetPatient",
                controllerName: "Patients",
                routeValues: new { practiceId, patientId = result.PatientId },
                value: result);
        }

        return Ok(result);
    }
}
