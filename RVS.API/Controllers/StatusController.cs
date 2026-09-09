using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Customer-facing status page accessed via magic-link token (<c>Spec X-1</c>).
/// All routes are anonymous — no authentication required — and rate-limited per IP.
/// The response is deliberately minimal: unit, submission date, current status, and the
/// servicing location's phone number. No conversation, messaging, or file exchange.
/// </summary>
[ApiController]
[Route("api/status")]
[AllowAnonymous]
[EnableRateLimiting("StatusEndpoint")]
public class StatusController : ControllerBase
{
    private readonly IGlobalCustomerAcctService _globalCustomerAcctService;
    private readonly ICustomerProfileService _customerProfileService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ILocationService _locationService;

    /// <summary>
    /// Initializes a new instance of <see cref="StatusController"/>.
    /// </summary>
    public StatusController(
        IGlobalCustomerAcctService globalCustomerAcctService,
        ICustomerProfileService customerProfileService,
        IServiceRequestService serviceRequestService,
        ILocationService locationService)
    {
        _globalCustomerAcctService = globalCustomerAcctService;
        _customerProfileService = customerProfileService;
        _serviceRequestService = serviceRequestService;
        _locationService = locationService;
    }

    /// <summary>
    /// Returns the customer's service request status across all dealerships.
    /// Validates the magic-link token and retrieves one minimal summary per request:
    /// unit, submission date, current status, and the servicing location's phone number
    /// (<c>Spec X-1</c>). Free-text problem descriptions are never included, nor logged.
    /// </summary>
    /// <param name="token">Magic-link token for customer identification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// GET /api/status/abc123def456
    /// </example>
    [HttpGet("{token}")]
    public async Task<ActionResult<CustomerStatusResponseDto>> GetStatus(string token, CancellationToken ct)
    {
        var acct = await _globalCustomerAcctService.ValidateMagicLinkTokenAsync(token, ct);

        var serviceRequests = new List<CustomerStatusItemResponseDto>();

        foreach (var link in acct.LinkedProfiles)
        {
            var profile = await _customerProfileService.GetByIdAsync(link.TenantId, link.ProfileId, ct);

            foreach (var srId in profile.ServiceRequestIds)
            {
                var sr = await _serviceRequestService.GetByIdAsync(link.TenantId, srId, ct);
                var locationPhone = await ResolveLocationPhoneAsync(link.TenantId, sr.LocationId, ct);

                serviceRequests.Add(sr.ToCustomerStatusItemDto(locationPhone));
            }
        }

        return Ok(new CustomerStatusResponseDto
        {
            ServiceRequests = serviceRequests
        });
    }

    private async Task<string?> ResolveLocationPhoneAsync(string tenantId, string locationId, CancellationToken ct)
    {
        try
        {
            var location = await _locationService.GetByIdAsync(tenantId, locationId, ct);
            return location.Phone;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
