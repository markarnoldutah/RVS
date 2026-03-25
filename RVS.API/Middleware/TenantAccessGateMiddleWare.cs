using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RVS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

public sealed partial class TenantAccessGateMiddleware
{
    private readonly RequestDelegate _next;

    // Allow these paths even if the tenant is disabled (so the UI can bootstrap and show message)
    private static readonly string[] AllowPrefixes =
    {
        "/api/tenants/config",
        "/api/service-requests/intake",
        "/health",
        "/swagger"
    };

    [GeneratedRegex(@"^/api/service-requests/[^/]+/status$", RegexOptions.IgnoreCase)]
    private static partial Regex StatusEndpointPattern();

    public TenantAccessGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, ITenantService tenantService)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;

        // Allowlist — prefix matches
        if (AllowPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        // Allowlist — pattern matches (e.g. /api/service-requests/{id}/status)
        if (StatusEndpointPattern().IsMatch(path))
        {
            await _next(ctx);
            return;
        }

        // Let auth handle unauthenticated requests
        if (!(ctx.User?.Identity?.IsAuthenticated ?? false))
        {
            await _next(ctx);
            return;
        }

        // TenantId should come from token claims (adjust claim types to match your auth setup)
        var tenantId =
            ctx.User.FindFirst("tenantId")?.Value
            ?? ctx.User.FindFirst("https://rvserviceflow.com/tenantId")?.Value;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                message = "Tenant context is missing from the access token.",
                errorId = Guid.NewGuid().ToString()
            });
            return;
        }

        // TODO consider saving access gate to Tables, cache etc to reduce RUs
        var gate = await tenantService.GetAccessGateAsync(tenantId);

        if (gate.LoginsEnabled == false)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                message = "Tenant disabled",
                errorId = Guid.NewGuid().ToString()
            });
            return;
        }

        await _next(ctx);
    }
}
