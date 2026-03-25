using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// Customer-facing status page accessed via magic-link token.
/// All routes are anonymous — no authentication required.
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

    /// <summary>
    /// Initializes a new instance of <see cref="StatusController"/>.
    /// </summary>
    public StatusController(
        IGlobalCustomerAcctService globalCustomerAcctService,
        ICustomerProfileService customerProfileService,
        IServiceRequestService serviceRequestService)
    {
        _globalCustomerAcctService = globalCustomerAcctService;
        _customerProfileService = customerProfileService;
        _serviceRequestService = serviceRequestService;
    }

    /// <summary>
    /// Returns the customer's service request status across all dealerships.
    /// Validates the magic-link token and retrieves service request summaries.
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

        var serviceRequests = new List<ServiceRequestSummaryResponseDto>();

        foreach (var link in acct.LinkedProfiles)
        {
            var profile = await _customerProfileService.GetByIdAsync(link.TenantId, link.ProfileId, ct);

            foreach (var srId in profile.ServiceRequestIds)
            {
                var sr = await _serviceRequestService.GetByIdAsync(link.TenantId, srId, ct);
                serviceRequests.Add(sr.ToSummaryDto());
            }
        }

        return Ok(new CustomerStatusResponseDto
        {
            FirstName = acct.FirstName,
            ServiceRequests = serviceRequests
        });
    }
}
