using System.Security.Claims;
using RVS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace RVS.API.Services;

/// <summary>
/// Implementation that retrieves user context from HttpContext claims.
/// </summary>
public sealed class HttpUserContextAccessor : IUserContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? TenantId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimsService.TenantIdClaimType)?.Value;
}