using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RVS.API.Tests.Integration;

/// <summary>
/// Minimal authenticated endpoint used only by the tenant access gate integration tests.
/// Its route (<c>/api/gate-probe</c>) is deliberately outside every
/// <c>TenantAccessGateMiddleware</c> allowlist prefix, so a 200 here means a request made it
/// through auth, authorization, and the gate to a controller.
/// </summary>
[ApiController]
[Route("api/gate-probe")]
[Authorize]
public sealed class GateProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { reached = true });
}
