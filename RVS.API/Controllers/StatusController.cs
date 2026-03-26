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
                var dto = sr.ToSummaryDto();

                dto = dto with { LocationName = await ResolveLocationNameAsync(link.TenantId, sr.LocationId, ct) };

                serviceRequests.Add(dto);
            }
        }

        return Ok(new CustomerStatusResponseDto
        {
            FirstName = acct.FirstName,
            ServiceRequests = serviceRequests
        });
    }

    private async Task<string?> ResolveLocationNameAsync(string tenantId, string locationId, CancellationToken ct)
    {
        try
        {
            var location = await _locationService.GetByIdAsync(tenantId, locationId, ct);
            if (!string.IsNullOrWhiteSpace(location.Address.City))
            {
                return string.IsNullOrWhiteSpace(location.Address.State)
                    ? location.Address.City.Trim()
                    : $"{location.Address.City.Trim()}, {location.Address.State.Trim()}";
            }

            return HumanizeSlug(location.Slug);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string HumanizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        var words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
        }

        return string.Join(' ', words);
    }
}
