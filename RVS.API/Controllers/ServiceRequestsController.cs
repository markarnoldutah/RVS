using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Controllers;

/// <summary>
/// Manages service request CRUD and search for the dealer dashboard.
/// </summary>
[ApiController]
[Route("api/dealerships/{dealershipId}/service-requests")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="ServiceRequestsController"/>.
    /// </summary>
    public ServiceRequestsController(IServiceRequestService service, ClaimsService claimsService)
    {
        _service = service;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Creates a new service request from the manager dashboard.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="request">The create request DTO.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "CanCreateServiceRequests")]
    public async Task<ActionResult<ServiceRequestDetailResponseDto>> Create(
        string dealershipId, [FromBody] ServiceRequestCreateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = request.ToEntity(tenantId, _claimsService.GetUserIdOrThrow());
        var created = await _service.CreateAsync(tenantId, entity, ct);

        return CreatedAtAction(nameof(GetById), new { dealershipId, srId = created.Id }, created.ToDetailDto());
    }

    /// <summary>
    /// Gets a single service request by its identifier.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{srId}")]
    [Authorize(Policy = "CanReadServiceRequests")]
    public async Task<ActionResult<ServiceRequestDetailResponseDto>> GetById(
        string dealershipId, string srId, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, srId, ct);

        return Ok(entity.ToDetailDto());
    }

    /// <summary>
    /// Searches service requests using filter criteria in the request body.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="request">Search filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("search")]
    [Authorize(Policy = "CanSearchServiceRequests")]
    public async Task<ActionResult<ServiceRequestSearchResultResponseDto>> Search(
        string dealershipId, [FromBody] ServiceRequestSearchRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var result = await _service.SearchAsync(tenantId, request, cancellationToken: ct);

        return Ok(new ServiceRequestSearchResultResponseDto
        {
            Results = result.ToSummaryPagedResult()
        });
    }

    /// <summary>
    /// Updates an existing service request.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="request">The update request DTO.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{srId}")]
    [Authorize(Policy = "CanUpdateServiceRequests")]
    public async Task<ActionResult<ServiceRequestDetailResponseDto>> Update(
        string dealershipId, string srId, [FromBody] ServiceRequestUpdateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var updated = await _service.UpdateAsync(tenantId, srId, request, ct);

        return Ok(updated.ToDetailDto());
    }

    /// <summary>
    /// Applies a shared repair outcome to multiple service requests in a single batch.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="request">Batch outcome request containing SR IDs and outcome fields.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("batch-outcome")]
    [Authorize(Policy = "CanUpdateServiceEvent")]
    public async Task<ActionResult<BatchOutcomeResponseDto>> BatchOutcome(
        string dealershipId, [FromBody] BatchOutcomeRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var result = await _service.BatchOutcomeAsync(tenantId, request, ct);

        return Ok(result);
    }

    /// <summary>
    /// Deletes a service request.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{srId}")]
    [Authorize(Policy = "CanDeleteServiceRequests")]
    public async Task<IActionResult> Delete(string dealershipId, string srId, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        await _service.DeleteAsync(tenantId, srId, ct);

        return NoContent();
    }

    /// <summary>
    /// Sets or clears the manager-authored customer status note (<c>Spec C-9</c>). The note renders
    /// on the anonymous customer status page alongside the current status. Send a null or blank
    /// <see cref="ServiceRequestStatusNoteRequestDto.Note"/> to clear it. An over-long note or one
    /// containing a blocked character returns <c>422</c>. The note is never written to logs.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="request">The note payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{srId}/status-note")]
    [Authorize(Policy = "CanUpdateServiceRequests")]
    public async Task<ActionResult<ServiceRequestDetailResponseDto>> SetStatusNote(
        string dealershipId, string srId, [FromBody] ServiceRequestStatusNoteRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var noteValidation = CustomerStatusNoteValidator.Validate(request.Note);
        if (!noteValidation.IsValid)
        {
            ModelState.AddModelError(nameof(request.Note), noteValidation.ErrorMessage!);
            return UnprocessableEntity(ModelState);
        }

        var updated = await _service.SetCustomerStatusNoteAsync(tenantId, srId, request.Note, ct);

        return Ok(updated.ToDetailDto());
    }

    /// <summary>
    /// Regenerates the request's service packet on demand (<c>Spec B-1</c>, issue #434). Resets
    /// packet-generation state to <c>Pending</c> and enqueues a fresh generation job; returns
    /// <c>202 Accepted</c> immediately — generation runs in the background.
    /// </summary>
    /// <param name="dealershipId">Dealership identifier (route segment).</param>
    /// <param name="srId">Service request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{srId}/packet/regenerate")]
    [Authorize(Policy = "CanUpdateServiceRequests")]
    public async Task<IActionResult> RegeneratePacket(string dealershipId, string srId, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        await _service.RegeneratePacketAsync(tenantId, srId, ct);

        return Accepted();
    }
}
