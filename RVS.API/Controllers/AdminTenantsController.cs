using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RVS.API.Mappers;
using RVS.API.Options;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Platform-admin tenant provisioning (Spec P-1 … P-8, issue #563), used from the hidden
/// <c>/admin</c> area of the Manager app.
/// <para>
/// The one controller that does <b>not</b> take the tenant from the caller's claims: the caller is
/// RVS staff acting on another tenant, so <c>tenantId</c> comes from the route. The
/// <c>PlatformAdmin</c> policy — the <c>platform:tenants:manage</c> permission <b>and</b> a caller on
/// <c>Admin:AllowedUserIds</c> — is what makes that safe.
/// </para>
/// <para>
/// Creates return <c>200 OK</c> rather than <c>201</c>: re-submitting is a supported retry that
/// may create nothing, and the response lists what each step did.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/tenants")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class AdminTenantsController : ControllerBase
{
    private readonly ITenantProvisioningService _service;
    private readonly IntakeUrlOptions _intakeUrlOptions;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminTenantsController"/>.
    /// </summary>
    public AdminTenantsController(ITenantProvisioningService service, IOptions<IntakeUrlOptions> intakeUrlOptions)
    {
        _service = service;
        _intakeUrlOptions = intakeUrlOptions.Value;
    }

    /// <summary>Lists every tenant with its status, plan, access gate and locations.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantSummaryResponseDto>>> List(CancellationToken ct)
    {
        var tenants = await _service.ListTenantsAsync(ct);

        return Ok(tenants.Select(t => t.ToSummaryDto(_intakeUrlOptions.BaseUrl)).ToList());
    }

    /// <summary>Edits a tenant's commercial details (status, plan, billing email, notes).</summary>
    [HttpPut("{tenantId}")]
    public async Task<ActionResult<TenantSummaryResponseDto>> Update(
        string tenantId, [FromBody] TenantUpdateRequestDto request, CancellationToken ct)
    {
        var overview = await _service.UpdateTenantAsync(tenantId, request, ct);

        return Ok(overview.ToSummaryDto(_intakeUrlOptions.BaseUrl));
    }

    /// <summary>Provisions a tenant, its first location and its first user (P-1). Safe to re-submit (P-6).</summary>
    [HttpPost]
    public async Task<ActionResult<TenantProvisioningResponseDto>> Create(
        [FromBody] TenantCreateRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateTenantAsync(request, ct);

        return Ok(result.ToResponseDto(_intakeUrlOptions.BaseUrl));
    }

    /// <summary>Adds a user to a tenant and returns a set-password link (P-2).</summary>
    [HttpPost("{tenantId}/users")]
    public async Task<ActionResult<TenantUserProvisioningResponseDto>> AddUser(
        string tenantId, [FromBody] TenantUserCreateRequestDto request, CancellationToken ct)
    {
        var result = await _service.AddUserAsync(tenantId, request, ct);

        return Ok(result.ToResponseDto());
    }

    /// <summary>Issues a new set-password link for an existing user of the tenant (P-3).</summary>
    [HttpPost("{tenantId}/users/{userId}/password-ticket")]
    public async Task<ActionResult<PasswordTicketResponseDto>> CreatePasswordTicket(
        string tenantId, string userId, CancellationToken ct)
    {
        var ticket = await _service.CreatePasswordTicketAsync(tenantId, userId, ct);

        return Ok(ticket.ToResponseDto(userId));
    }

    /// <summary>Enables or disables logins for a tenant (P-4).</summary>
    [HttpPut("{tenantId}/access-gate")]
    public async Task<ActionResult<TenantSummaryResponseDto>> SetAccessGate(
        string tenantId, [FromBody] TenantAccessGateUpdateRequestDto request, CancellationToken ct)
    {
        var overview = await _service.SetAccessGateAsync(tenantId, request, ct);

        return Ok(overview.ToSummaryDto(_intakeUrlOptions.BaseUrl));
    }

    /// <summary>Adds a location to a tenant (P-5).</summary>
    [HttpPost("{tenantId}/locations")]
    public async Task<ActionResult<TenantLocationProvisioningResponseDto>> AddLocation(
        string tenantId, [FromBody] TenantLocationCreateRequestDto request, CancellationToken ct)
    {
        var location = await _service.AddLocationAsync(tenantId, request, ct);

        return Ok(location.ToProvisioningResponseDto(_intakeUrlOptions.BaseUrl));
    }
}
