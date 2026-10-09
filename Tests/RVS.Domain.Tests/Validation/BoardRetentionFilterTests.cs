using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="BoardRetentionFilter"/> — which requests the manager board (Spec C-10)
/// shows.
///
/// Contract under test: every open request is on the board, however old; a closed one
/// (Completed or Cancelled, the Done column) stays only while it was last changed less than
/// <see cref="BoardRetentionFilter.DoneColumnWindowDays"/> days ago. Older closed work is
/// reached through <c>/service-requests</c>.
/// </summary>
public class BoardRetentionFilterTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DoneColumnWindowDays_ShouldBeSixty()
    {
        BoardRetentionFilter.DoneColumnWindowDays.Should().Be(60);
    }

    [Theory]
    [InlineData("New")]
    [InlineData("InProgress")]
    [InlineData("WaitingOnParts")]
    [InlineData("WaitingOnCustomer")]
    public void IsOnBoard_WhenOpen_ShouldBeTrueRegardlessOfAge(string status)
    {
        var longAgo = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        BoardRetentionFilter.IsOnBoard(status, longAgo, longAgo, NowUtc).Should().BeTrue();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void IsOnBoard_WhenClosedWithinWindow_ShouldBeTrue(string status)
    {
        var updated = NowUtc.AddDays(-59);

        BoardRetentionFilter.IsOnBoard(status, updated.AddDays(-30), updated, NowUtc).Should().BeTrue();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void IsOnBoard_WhenClosedExactlySixtyDaysAgo_ShouldBeFalse(string status)
    {
        var updated = NowUtc.AddDays(-60);

        BoardRetentionFilter.IsOnBoard(status, updated.AddDays(-30), updated, NowUtc).Should().BeFalse();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void IsOnBoard_WhenClosedBeforeWindow_ShouldBeFalse(string status)
    {
        var updated = NowUtc.AddDays(-61);

        BoardRetentionFilter.IsOnBoard(status, updated.AddDays(-30), updated, NowUtc).Should().BeFalse();
    }

    [Fact]
    public void IsOnBoard_WhenCreatedLongAgoButClosedRecently_ShouldBeTrue()
    {
        var created = NowUtc.AddDays(-120);
        var updated = NowUtc.AddDays(-2);

        BoardRetentionFilter.IsOnBoard("Completed", created, updated, NowUtc).Should().BeTrue();
    }

    [Fact]
    public void IsOnBoard_WhenClosedAndNeverUpdated_ShouldFallBackToCreatedDate()
    {
        BoardRetentionFilter.IsOnBoard("Completed", NowUtc.AddDays(-10), null, NowUtc).Should().BeTrue();
        BoardRetentionFilter.IsOnBoard("Completed", NowUtc.AddDays(-90), null, NowUtc).Should().BeFalse();
    }

    [Fact]
    public void IsOnBoard_WhenTimestampsAreUnspecifiedKind_ShouldTreatThemAsUtc()
    {
        var updated = DateTime.SpecifyKind(NowUtc.AddDays(-59), DateTimeKind.Unspecified);

        BoardRetentionFilter.IsOnBoard("Completed", updated, updated, NowUtc).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsOnBoard_WhenStatusIsBlank_ShouldThrowArgumentException(string? status)
    {
        var act = () => BoardRetentionFilter.IsOnBoard(status!, NowUtc, null, NowUtc);

        act.Should().Throw<ArgumentException>();
    }
}
