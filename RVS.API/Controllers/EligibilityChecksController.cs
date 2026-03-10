using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Provides eligibility check operations for encounters.
/// Eligibility checks are embedded within Encounters, which are embedded in the Patient aggregate.
/// </summary>
[ApiController]
[Route("api/practices/{practiceId}/patients/{patientId}/encounters/{encounterId}/eligibility-checks")]
[Authorize]
public class EligibilityChecksController : ControllerBase
{
    private readonly IEligibilityCheckService _eligibilityCheckService;
    private readonly ClaimsService _claimsService;

    public EligibilityChecksController(
        IEligibilityCheckService eligibilityCheckService,
        ClaimsService claimsService)
    {
        _eligibilityCheckService = eligibilityCheckService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Run an eligibility check for a specific coverage enrollment.
    /// timeoutSeconds overrides Availity call timeout only.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<EligibilityCheckResponseDto>> RunEligibilityCheck(
        string practiceId,
        string patientId,
        string encounterId,
        [FromBody] EligibilityCheckRequestDto request,
        CancellationToken cancellationToken,
        [FromQuery] int? timeoutSeconds = null)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var timeout = timeoutSeconds is null ? (System.TimeSpan?)null : System.TimeSpan.FromSeconds(timeoutSeconds.Value);

        var check = await _eligibilityCheckService.RunEligibilityCheckAsync(
            tenantId,
            practiceId,
            patientId,
            encounterId,
            request,
            cancellationToken,
            timeout);

        return Ok(check.ToDto());
    }

    /// <summary>
    /// Gets all eligibility checks for an encounter.
    /// Supports optional selective polling via query parameter.
    /// 
    /// Query parameters:
    /// - pollCheckIds: Comma-separated list of check IDs to poll (e.g., "check1,check2,check3")
    /// 
    /// Examples:
    /// - GET /eligibility-checks (returns current state, no polling, ~1 RU)
    /// - GET /eligibility-checks?pollCheckIds=abc123,def456 (polls 2 checks, ~4 RU)
    /// 
    /// RU Cost: ~1 RU without polling, ~1 RU + ~1.5 RU per polled check with polling
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EligibilityCheckSummaryResponseDto>>> GetEligibilityChecks(
        string practiceId,
        string patientId,
        string encounterId,
        [FromQuery] string? pollCheckIds = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        List<Domain.Entities.EligibilityCheckEmbedded> checks;

        // Parse comma-separated check IDs if provided
        if (!string.IsNullOrWhiteSpace(pollCheckIds))
        {
            var checkIdList = pollCheckIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (checkIdList.Count > 0)
            {
                // Use selective polling API
                checks = await _eligibilityCheckService.GetEligibilityChecksWithSelectivePollingAsync(
                    tenantId, 
                    practiceId, 
                    patientId, 
                    encounterId, 
                    checkIdList,
                    cancellationToken);
            }
            else
            {
                // Empty list after parsing, fall back to standard get
                checks = await _eligibilityCheckService.GetEligibilityChecksAsync(
                    tenantId, practiceId, patientId, encounterId);
            }
        }
        else
        {
            // No polling requested, just get current state
            checks = await _eligibilityCheckService.GetEligibilityChecksAsync(
                tenantId, practiceId, patientId, encounterId);
        }

        var dtos = checks.Select(c => c.ToSummaryDto()).ToList();
        return Ok(dtos);
    }

    [HttpGet("{eligibilityCheckId}")]
    public async Task<ActionResult<EligibilityCheckResponseDto>> GetEligibilityCheck(
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var check = await _eligibilityCheckService.GetEligibilityCheckAsync(
            tenantId,
            practiceId,
            patientId,
            encounterId,
            eligibilityCheckId);

        return Ok(check.ToDto());
    }

    [HttpPost("{eligibilityCheckId}/coverage-lines")]
    public async Task<ActionResult<CoverageLineResponseDto>> AddCoverageLine(
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        [FromBody] CoverageLineAddRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var coverageLine = await _eligibilityCheckService.AddCoverageLineAsync(
            tenantId,
            practiceId,
            patientId,
            encounterId,
            eligibilityCheckId,
            request);

        return Ok(coverageLine.ToCoverageLineDto());
    }

    [HttpPost("{eligibilityCheckId}/payloads")]
    public async Task<ActionResult> AddEligibilityPayload(
        string practiceId,
        string patientId,
        string encounterId,
        string eligibilityCheckId,
        [FromBody] EligibilityPayloadAddRequestDto request)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        await _eligibilityCheckService.AddEligibilityPayloadAsync(
            tenantId,
            practiceId,
            patientId,
            encounterId,
            eligibilityCheckId,
            request);

        return NoContent();
    }
}
