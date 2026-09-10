using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RVS.API.Tests.Integration;

/// <summary>
/// Stand-in authentication handler for the tenant access gate integration tests. It shapes
/// the request principal from headers so a single test host can exercise authenticated,
/// unauthenticated, and missing-tenant-claim requests through the real pipeline:
/// <list type="bullet">
///   <item><description><c>X-Test-Authenticated: true</c> — request is authenticated.</description></item>
///   <item><description><c>X-Test-TenantId: &lt;value&gt;</c> — adds the namespaced tenantId claim the gate reads.</description></item>
/// </list>
/// Without the <c>X-Test-Authenticated</c> header the handler reports no result, so
/// <c>UseAuthentication</c>/<c>UseAuthorization</c> treat the caller as anonymous.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTest";
    public const string TenantIdClaimType = "https://rvserviceflow.com/tenantId";
    public const string AuthenticatedHeader = "X-Test-Authenticated";
    public const string TenantIdHeader = "X-Test-TenantId";
    public const string PermissionsHeader = "X-Test-Permissions";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers[AuthenticatedHeader] != "true")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "usr_integration_test") };

        var tenantId = Request.Headers[TenantIdHeader].ToString();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim(TenantIdClaimType, tenantId));
        }

        var permissions = Request.Headers[PermissionsHeader].ToString();
        if (!string.IsNullOrWhiteSpace(permissions))
        {
            foreach (var permission in permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("permissions", permission));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
