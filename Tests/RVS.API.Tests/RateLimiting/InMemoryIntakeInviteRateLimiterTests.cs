using FluentAssertions;
using RVS.API.Options;
using RVS.API.RateLimiting;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.RateLimiting;

/// <summary>
/// Tests for <see cref="InMemoryIntakeInviteRateLimiter"/> — the per-advisor, per-location and
/// per-tenant caps on texted intake invites (<c>Spec A-14</c>, issue #663).
/// </summary>
public class InMemoryIntakeInviteRateLimiterTests
{
    private const string Tenant = "ten_test";
    private const string Location = "loc_slc";
    private const string Advisor = "auth0|advisor";

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));

    private InMemoryIntakeInviteRateLimiter CreateLimiter(int perAdvisor = 100, int perLocation = 100, int perTenant = 100) =>
        new(Microsoft.Extensions.Options.Options.Create(new IntakeInviteOptions
        {
            MaxPerAdvisorPerHour = perAdvisor,
            MaxPerLocationPerHour = perLocation,
            MaxPerTenantPerHour = perTenant,
        }), _time);

    [Fact]
    public void TryAcquire_UnderEveryLimit_ShouldAllow()
    {
        CreateLimiter().TryAcquire(Tenant, Location, Advisor).Should().Be(IntakeInviteRateLimitResult.Allowed);
    }

    [Fact]
    public void TryAcquire_PastTheAdvisorLimit_ShouldReportTheAdvisorLimit()
    {
        var sut = CreateLimiter(perAdvisor: 2);
        sut.TryAcquire(Tenant, Location, Advisor);
        sut.TryAcquire(Tenant, Location, Advisor);

        sut.TryAcquire(Tenant, Location, Advisor).Should().Be(IntakeInviteRateLimitResult.AdvisorLimitReached);
    }

    [Fact]
    public void TryAcquire_PastTheAdvisorLimit_ShouldNotBlockAnotherAdvisor()
    {
        var sut = CreateLimiter(perAdvisor: 1);
        sut.TryAcquire(Tenant, Location, Advisor);

        sut.TryAcquire(Tenant, Location, "auth0|other").Should().Be(IntakeInviteRateLimitResult.Allowed);
    }

    [Fact]
    public void TryAcquire_PastTheLocationLimit_ShouldReportTheLocationLimitAcrossAdvisors()
    {
        var sut = CreateLimiter(perLocation: 2);
        sut.TryAcquire(Tenant, Location, "auth0|a");
        sut.TryAcquire(Tenant, Location, "auth0|b");

        sut.TryAcquire(Tenant, Location, "auth0|c").Should().Be(IntakeInviteRateLimitResult.LocationLimitReached);
        sut.TryAcquire(Tenant, "loc_other", "auth0|c").Should().Be(IntakeInviteRateLimitResult.Allowed);
    }

    [Fact]
    public void TryAcquire_PastTheTenantLimit_ShouldReportTheTenantLimitAcrossLocations()
    {
        var sut = CreateLimiter(perTenant: 2);
        sut.TryAcquire(Tenant, "loc_a", "auth0|a");
        sut.TryAcquire(Tenant, "loc_b", "auth0|b");

        sut.TryAcquire(Tenant, "loc_c", "auth0|c").Should().Be(IntakeInviteRateLimitResult.TenantLimitReached);
        sut.TryAcquire("ten_other", "loc_c", "auth0|c").Should().Be(IntakeInviteRateLimitResult.Allowed);
    }

    [Fact]
    public void TryAcquire_WhenDenied_ShouldTakeNothingFromTheOtherAllowances()
    {
        // Advisor A is capped at 1; their denied second attempt must not eat the location's slot.
        var sut = CreateLimiter(perAdvisor: 1, perLocation: 2);
        sut.TryAcquire(Tenant, Location, "auth0|a");
        sut.TryAcquire(Tenant, Location, "auth0|a").Should().Be(IntakeInviteRateLimitResult.AdvisorLimitReached);

        sut.TryAcquire(Tenant, Location, "auth0|b").Should().Be(IntakeInviteRateLimitResult.Allowed);
    }

    [Fact]
    public void TryAcquire_AnHourAfterASend_ShouldFreeItsSlot()
    {
        var sut = CreateLimiter(perAdvisor: 1);
        sut.TryAcquire(Tenant, Location, Advisor);

        _time.Advance(TimeSpan.FromHours(1));

        sut.TryAcquire(Tenant, Location, Advisor).Should().Be(IntakeInviteRateLimitResult.Allowed);
    }

    [Theory]
    [InlineData(null, Location, Advisor)]
    [InlineData(Tenant, "", Advisor)]
    [InlineData(Tenant, Location, " ")]
    public void TryAcquire_WhenAnyKeyIsBlank_ShouldThrowArgumentException(string? tenant, string? location, string? advisor)
    {
        var act = () => CreateLimiter().TryAcquire(tenant!, location!, advisor!);

        act.Should().Throw<ArgumentException>();
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
