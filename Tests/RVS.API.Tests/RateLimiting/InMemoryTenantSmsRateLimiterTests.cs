using FluentAssertions;
using RVS.API.Options;
using RVS.API.RateLimiting;

namespace RVS.API.Tests.RateLimiting;

public class InMemoryTenantSmsRateLimiterTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));

    private InMemoryTenantSmsRateLimiter CreateLimiter(int maxPerHour) =>
        new(Microsoft.Extensions.Options.Options.Create(new SmsOptions { MaxMessagesPerTenantPerHour = maxPerHour }), _time);

    [Fact]
    public void TryAcquire_UpToTheHourlyLimit_ShouldAllowEverySend()
    {
        var sut = CreateLimiter(3);

        var results = Enumerable.Range(0, 3).Select(_ => sut.TryAcquire("ten_test")).ToList();

        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Fact]
    public void TryAcquire_PastTheHourlyLimit_ShouldDeny()
    {
        var sut = CreateLimiter(3);
        for (var i = 0; i < 3; i++)
        {
            sut.TryAcquire("ten_test");
        }

        sut.TryAcquire("ten_test").Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_DeniedAttempts_ShouldNotConsumeTheWindow()
    {
        var sut = CreateLimiter(1);
        sut.TryAcquire("ten_test");
        _time.Advance(TimeSpan.FromMinutes(30));
        sut.TryAcquire("ten_test").Should().BeFalse();

        // An hour after the only accepted send, the slot frees even though a denied attempt came later.
        _time.Advance(TimeSpan.FromMinutes(30));

        sut.TryAcquire("ten_test").Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_WhenTheOldestSendLeavesTheWindow_ShouldAllowAgain()
    {
        var sut = CreateLimiter(2);
        sut.TryAcquire("ten_test");
        _time.Advance(TimeSpan.FromMinutes(20));
        sut.TryAcquire("ten_test");

        _time.Advance(TimeSpan.FromMinutes(40));    // first send is now exactly an hour old

        sut.TryAcquire("ten_test").Should().BeTrue();
        sut.TryAcquire("ten_test").Should().BeFalse("the second send is still inside the window");
    }

    [Fact]
    public void TryAcquire_ShouldCountEachTenantSeparately()
    {
        var sut = CreateLimiter(1);
        sut.TryAcquire("ten_a").Should().BeTrue();

        sut.TryAcquire("ten_b").Should().BeTrue();
        sut.TryAcquire("ten_a").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TryAcquire_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var sut = CreateLimiter(3);

        var act = () => sut.TryAcquire(tenantId!);

        act.Should().Throw<ArgumentException>();
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
