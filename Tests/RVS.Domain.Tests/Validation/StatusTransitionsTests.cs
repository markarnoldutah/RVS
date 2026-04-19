using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class StatusTransitionsTests
{
    [Theory]
    [InlineData("New", "InProgress")]
    [InlineData("New", "Completed")]
    [InlineData("New", "Cancelled")]
    [InlineData("New", "WaitingOnParts")]
    [InlineData("InProgress", "New")]
    [InlineData("InProgress", "Completed")]
    [InlineData("InProgress", "Cancelled")]
    [InlineData("InProgress", "WaitingOnParts")]
    [InlineData("WaitingOnParts", "New")]
    [InlineData("WaitingOnParts", "InProgress")]
    [InlineData("WaitingOnParts", "Completed")]
    [InlineData("WaitingOnParts", "Cancelled")]
    [InlineData("Completed", "New")]
    [InlineData("Completed", "InProgress")]
    [InlineData("Completed", "Cancelled")]
    [InlineData("Completed", "WaitingOnParts")]
    [InlineData("Cancelled", "New")]
    [InlineData("Cancelled", "InProgress")]
    [InlineData("Cancelled", "Completed")]
    [InlineData("Cancelled", "WaitingOnParts")]
    public void IsValid_AllowedTransition_ReturnsTrue(string from, string to)
    {
        StatusTransitions.IsValid(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData("New", "New")]
    [InlineData("InProgress", "InProgress")]
    [InlineData("WaitingOnParts", "WaitingOnParts")]
    [InlineData("Completed", "Completed")]
    [InlineData("Cancelled", "Cancelled")]
    public void IsValid_SameStatus_ReturnsFalse(string from, string to)
    {
        StatusTransitions.IsValid(from, to).Should().BeFalse();
    }

    [Fact]
    public void IsValid_UnknownFromStatus_ReturnsFalse()
    {
        StatusTransitions.IsValid("Unknown", "New").Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "InProgress")]
    [InlineData("New", null)]
    [InlineData("", "InProgress")]
    [InlineData("New", "")]
    [InlineData("  ", "InProgress")]
    [InlineData("New", "  ")]
    public void IsValid_NullOrWhitespace_ThrowsArgumentException(string? from, string? to)
    {
        var act = () => StatusTransitions.IsValid(from!, to!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetAllowedTargets_New_ReturnsAllOtherStatuses()
    {
        var targets = StatusTransitions.GetAllowedTargets("New");

        targets.Should().BeEquivalentTo(["InProgress", "Completed", "Cancelled", "WaitingOnParts"]);
    }

    [Fact]
    public void GetAllowedTargets_Completed_ReturnsAllOtherStatuses()
    {
        var targets = StatusTransitions.GetAllowedTargets("Completed");

        targets.Should().BeEquivalentTo(["New", "InProgress", "Cancelled", "WaitingOnParts"]);
    }

    [Fact]
    public void GetAllowedTargets_UnknownStatus_ReturnsEmpty()
    {
        var targets = StatusTransitions.GetAllowedTargets("Unknown");

        targets.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetAllowedTargets_NullOrWhitespace_ThrowsArgumentException(string? status)
    {
        var act = () => StatusTransitions.GetAllowedTargets(status!);

        act.Should().Throw<ArgumentException>();
    }
}
