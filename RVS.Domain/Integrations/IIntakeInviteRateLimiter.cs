namespace RVS.Domain.Integrations;

/// <summary>
/// Caps texted intake invites per advisor, per location and per tenant in any rolling hour
/// (<c>Spec A-14</c>, issue #663). The caps are configuration (<c>IntakeInvites</c> section),
/// not constants. This sits in front of <see cref="ITenantSmsRateLimiter"/>, which still caps
/// every SMS the tenant sends.
/// </summary>
public interface IIntakeInviteRateLimiter
{
    /// <summary>
    /// Takes one invite from all three allowances at once, or, when any of them is spent, takes
    /// nothing and reports the first one that is.
    /// </summary>
    /// <param name="tenantId">Tenant the invite is sent for.</param>
    /// <param name="locationId">Location the invite is sent for.</param>
    /// <param name="advisorUserId">Advisor sending the invite.</param>
    IntakeInviteRateLimitResult TryAcquire(string tenantId, string locationId, string advisorUserId);
}

/// <summary>Outcome of <see cref="IIntakeInviteRateLimiter.TryAcquire"/>.</summary>
public enum IntakeInviteRateLimitResult
{
    /// <summary>The invite may be sent; one slot was taken from each allowance.</summary>
    Allowed,

    /// <summary>The advisor's hourly allowance is spent.</summary>
    AdvisorLimitReached,

    /// <summary>The location's hourly allowance is spent.</summary>
    LocationLimitReached,

    /// <summary>The tenant's hourly allowance is spent.</summary>
    TenantLimitReached,
}
