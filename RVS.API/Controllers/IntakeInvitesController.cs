using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Advisor intake invites for a location (<c>Spec A-14</c>, issue #663): text a caller a
/// prefilled, single-use intake link, or mint one to fill in during the call. Backs the manager
/// app's Send intake link dialog.
///
/// Every action requires <c>intake-invites:send</c>. Being signed in to the manager app is not enough.
/// </summary>
[ApiController]
[Route("api/locations/{locationId}/intake-invites")]
[Authorize]
public class IntakeInvitesController : ControllerBase
{
    private readonly IIntakeInviteService _service;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeInvitesController"/>.
    /// </summary>
    public IntakeInvitesController(IIntakeInviteService service, ClaimsService claimsService)
    {
        _service = service;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Creates an invite. A texted invite requires the caller's consent and is refused with 409
    /// while texting is disabled or when the number has opted out, and with 429 past a rate
    /// limit. A self-entry invite (<c>selfEntry: true</c>) texts nobody, works while texting is
    /// disabled, and returns <c>intakeUrl</c> for the advisor to open.
    /// </summary>
    /// <param name="locationId">Location whose intake form the invite opens.</param>
    /// <param name="request">The invite to create.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "CanSendIntakeInvites")]
    public async Task<ActionResult<IntakeInviteDetailResponseDto>> Create(
        string locationId, [FromBody] IntakeInviteCreateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var result = await _service.CreateAsync(tenantId, locationId, request, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { locationId, id = result.Invite.Id },
            result.Invite.ToDetailDto(result.IntakeUrl));
    }

    /// <summary>
    /// The current advisor's invites at this location for the current shift, newest first.
    /// </summary>
    /// <param name="locationId">Location the invites were created for.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "CanSendIntakeInvites")]
    public async Task<ActionResult<IReadOnlyList<IntakeInviteSummaryResponseDto>>> ListRecent(
        string locationId, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var invites = await _service.ListRecentForCurrentAdvisorAsync(tenantId, locationId, ct);

        return Ok(invites.Select(i => i.ToSummaryDto()).ToList());
    }

    /// <summary>
    /// What the send dialog can offer right now. Read on open so the dialog can say texting is
    /// not enabled yet, rather than present a Send button that can only 409.
    ///
    /// It hangs off the location route so the dialog's calls stay under one client and one
    /// policy, but the answer is environment-wide: <c>locationId</c> does not narrow it, and so
    /// is not bound.
    /// </summary>
    [HttpGet("capability")]
    [Authorize(Policy = "CanSendIntakeInvites")]
    public ActionResult<IntakeInviteCapabilityResponseDto> GetCapability()
    {
        // Route-matched ahead of GetById: a literal segment outranks "{id}".
        _claimsService.GetTenantIdOrThrow();

        return Ok(_service.GetCapability().ToDto());
    }

    /// <summary>
    /// One invite, for the send dialog's inline delivery status.
    /// </summary>
    /// <param name="locationId">Location the invite belongs to.</param>
    /// <param name="id">Invite id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanSendIntakeInvites")]
    public async Task<ActionResult<IntakeInviteDetailResponseDto>> GetById(
        string locationId, string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var invite = await _service.GetByIdAsync(tenantId, locationId, id, ct);

        return Ok(invite.ToDetailDto());
    }
}
