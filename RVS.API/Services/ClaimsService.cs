using System;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RVS.API.Services;

/// <summary>
/// Service for extracting and validating claims from the current user.
/// Provides centralized access to custom claim types.
/// </summary>
public sealed class ClaimsService
{
    public const string TenantIdClaimType = "https://rvserviceflow.com/tenantId";
    public const string LocationIdsClaimType = "app_metadata/locationIds";

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

    /// <summary>
    /// Retrieves the location IDs from the current user's claims.
    /// Throws <see cref="UnauthorizedAccessException"/> if the claim is missing or empty.
    /// </summary>
    /// <returns>A read-only list of location IDs.</returns>
    public IReadOnlyList<string> GetLocationIdsOrThrow()
    {
        var raw = User.FindFirst(LocationIdsClaimType)?.Value;

        if (string.IsNullOrWhiteSpace(raw))
            throw new UnauthorizedAccessException("locationIds claim is missing.");

        return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
    }

    /// <summary>
    /// Checks whether the current user has access to the specified location.
    /// </summary>
    /// <param name="locationId">The location ID to check access for.</param>
    /// <returns><c>true</c> if the user has access; otherwise, <c>false</c>.</returns>
    public bool HasAccessToLocation(string locationId) =>
        GetLocationIdsOrThrow().Contains(locationId, StringComparer.OrdinalIgnoreCase);
}
