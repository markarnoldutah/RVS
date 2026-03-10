using System;
using System.Linq;
using System.Threading.Tasks;
using RVS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

public sealed class TenantAccessGateMiddleware
{
    private readonly RequestDelegate _next;

    // Allow these paths even if the tenant is disabled (so the UI can bootstrap and show message)
    private static readonly string[] AllowPrefixes =
    {
        "/api/tenants/config",
        "/health",
        "/swagger"
    };

    public TenantAccessGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, ITenantService tenantService)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;

        // Allowlist
        if (AllowPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
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
            ?? ctx.User.FindFirst("http://benefetch.com/tenantId")?.Value;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "TenantIdMissing",
                message = "Tenant context is missing from the access token."
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
                error = "TenantDisabled",
                reason = gate.DisabledReason ?? "Disabled",
                message = gate.DisabledMessage ?? "Tenant access is currently disabled.",
                support = gate.SupportContactEmail
            });
            return;
        }

        await _next(ctx);
    }
}
