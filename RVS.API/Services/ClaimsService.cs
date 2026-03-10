using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace RVS.API.Services;

/// <summary>
/// Service for extracting and validating claims from the current user.
/// Provides centralized access to custom claim types.
/// </summary>
public sealed class ClaimsService
{

    public const string TenantIdClaimType = "http://benefetch.com/tenantId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current user's ClaimsPrincipal.
    /// </summary>
    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("User is not available in the current context.");

    /// <summary>
    /// Retrieves the tenant ID from the current user's claims.
    /// Throws UnauthorizedAccessException if the claim is missing or empty.
    /// </summary>
    public string GetTenantIdOrThrow()
    {
        var tenantId = User.FindFirst(TenantIdClaimType)?.Value;

        if (string.IsNullOrWhiteSpace(tenantId))
            throw new UnauthorizedAccessException("Tenant identifier is missing.");

        return tenantId;
    }
}
