using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// The <c>go.rvintake.com</c> short-link redirect (<c>Spec A-13</c>, issue #599). Anonymous, by
/// definition — this is the first thing a customer touches, and it sits in front of the intake
/// form rather than behind it.
///
/// Every distribution path routes through here: the QR sticker, the link an advisor texts, the
/// URL printed on a business card. That is the point — one path in means one place a channel
/// can be observed and one place a hit is logged. The <c>src</c> query parameter names the
/// channel and is optional: printed material cannot carry a query string, so its absence is
/// recorded as <c>print</c>.
///
/// Two routes, one action. <c>/{locationSlug}</c> is the real one — it is what
/// <c>go.rvintake.com/nova-hurricane</c> resolves to, and the shortness is the whole point of
/// owning the domain. <c>/go/{locationSlug}</c> is the same endpoint reachable on the API's own
/// hostname, for testing and for any environment that has no <c>go</c> host bound yet. Both
/// carry a slug-shaped route constraint so nothing else served at the API root — <c>/health</c>,
/// <c>/favicon.ico</c>, a stray <c>/swagger</c> — is swallowed by the redirect.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("RedirectEndpoint")]
public class GoController : ControllerBase
{
    /// <summary>
    /// Route constraint matching the slug format enforced by
    /// <c>RVS.Domain.Validation.SlugValidator</c>: lowercase alphanumerics and hyphens, up to
    /// 64 characters.
    /// </summary>
    private const string SlugRoute = "{locationSlug:regex(^[[a-z0-9-]]{{1,64}}$)}";

    private readonly IIntakeRedirectService _redirectService;

    /// <summary>
    /// Initializes a new instance of <see cref="GoController"/>.
    /// </summary>
    public GoController(IIntakeRedirectService redirectService)
    {
        _redirectService = redirectService;
    }

    /// <summary>
    /// Redirects to a location's intake form, recording the channel the customer arrived
    /// through. Always redirects: an unknown slug, an unrecognised <c>src</c>, or a hit-log
    /// failure all still send the customer on.
    /// </summary>
    /// <param name="locationSlug">Location slug from the short link path.</param>
    /// <param name="src">Channel tag — <c>qr</c>, <c>textrepl</c>, <c>quickreply</c>, <c>mgrapp</c>, or any other. Optional; absent means print.</param>
    /// <param name="inv">A-14 advisor invite token, passed through to the intake app (issue #663). Optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// GET https://go.rvintake.com/nova-hurricane?src=qr
    /// </example>
    [HttpGet("/" + SlugRoute)]
    [HttpGet("/go/" + SlugRoute)]
    public async Task<IActionResult> RedirectToIntake(
        string locationSlug, [FromQuery] string? src = null, [FromQuery] string? inv = null, CancellationToken ct = default)
    {
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _redirectService.ResolveAsync(locationSlug, src, userAgent, inv, ct);

        // 302, never 301, and never cached: a permanent or cached redirect is followed by the
        // client without touching this endpoint again, so every later tap would go unlogged.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        return Redirect(result.TargetUrl);
    }
}
