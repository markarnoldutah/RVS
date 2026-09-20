using FluentAssertions;
using RVS.API.RateLimiting;

namespace RVS.API.Tests.RateLimiting;

/// <summary>
/// Tests for <see cref="InMemoryInboundSmsDeduplicator"/> — stops a redelivered inbound event
/// from sending the HELP reply twice (issue #665).
/// </summary>
public class InMemoryInboundSmsDeduplicatorTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 19, 16, 0, 0, TimeSpan.Zero));

    private InMemoryInboundSmsDeduplicator CreateDeduplicator() => new(_time);

    [Fact]
    public void TryBeginHandling_WhenMessageIsNew_ShouldBeTrue()
    {
        CreateDeduplicator().TryBeginHandling("Incoming_1").Should().BeTrue();
    }

    [Fact]
    public void TryBeginHandling_WhenMessageRepeats_ShouldBeFalse()
    {
        // Event Grid delivers at least once; the customer must not get two replies.
        var deduplicator = CreateDeduplicator();
        deduplicator.TryBeginHandling("Incoming_1");

        deduplicator.TryBeginHandling("Incoming_1").Should().BeFalse();
    }

    [Fact]
    public void TryBeginHandling_ShouldTrackMessagesIndependently()
    {
        var deduplicator = CreateDeduplicator();
        deduplicator.TryBeginHandling("Incoming_1");

        deduplicator.TryBeginHandling("Incoming_2").Should().BeTrue();
    }

    [Fact]
    public void TryBeginHandling_WhenTheRetryWindowHasPassed_ShouldAllowItAgain()
    {
        // Past 24 hours Event Grid has given up retrying, so a repeat is a real second text.
        var deduplicator = CreateDeduplicator();
        deduplicator.TryBeginHandling("Incoming_1");

        _time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        deduplicator.TryBeginHandling("Incoming_1").Should().BeTrue();
    }

    [Fact]
    public void TryBeginHandling_WhenManyMessagesArrive_ShouldNotGrowWithoutBound()
    {
        var deduplicator = CreateDeduplicator();
        for (var i = 0; i < 12_000; i++)
        {
            deduplicator.TryBeginHandling($"Incoming_{i}");
        }

        deduplicator.Count.Should().BeLessThanOrEqualTo(InMemoryInboundSmsDeduplicator.MaxTrackedMessages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryBeginHandling_WhenMessageIdIsMissing_ShouldThrowArgumentException(string? id)
    {
        var act = () => CreateDeduplicator().TryBeginHandling(id!);

        act.Should().Throw<ArgumentException>();
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
