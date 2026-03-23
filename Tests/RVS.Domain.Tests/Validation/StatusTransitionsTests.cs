using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class StatusTransitionsTests
{
    [Theory]
    [InlineData("New", "InProgress")]
    [InlineData("New", "Cancelled")]
    [InlineData("InProgress", "Completed")]
    [InlineData("InProgress", "Cancelled")]
    [InlineData("InProgress", "WaitingOnParts")]
    [InlineData("WaitingOnParts", "InProgress")]
    [InlineData("WaitingOnParts", "Cancelled")]
    [InlineData("Cancelled", "New")]
    public void IsValid_AllowedTransition_ReturnsTrue(string from, string to)
    {
        StatusTransitions.IsValid(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData("New", "Completed")]
    [InlineData("New", "WaitingOnParts")]
    [InlineData("New", "New")]
    [InlineData("InProgress", "New")]
    [InlineData("InProgress", "InProgress")]
    [InlineData("WaitingOnParts", "Completed")]
    [InlineData("WaitingOnParts", "WaitingOnParts")]
    [InlineData("WaitingOnParts", "New")]
    [InlineData("Completed", "New")]
    [InlineData("Completed", "InProgress")]
    [InlineData("Completed", "Cancelled")]
    [InlineData("Cancelled", "InProgress")]
    [InlineData("Cancelled", "Completed")]
    [InlineData("Cancelled", "Cancelled")]
    public void IsValid_DisallowedTransition_ReturnsFalse(string from, string to)
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
    public void IsValid_CompletedIsTerminal_ReturnsFalseForAllTargets()
    {
        string[] allStatuses = ["New", "InProgress", "WaitingOnParts", "Completed", "Cancelled"];

        foreach (var target in allStatuses)
        {
            StatusTransitions.IsValid("Completed", target).Should().BeFalse(
                because: $"Completed should not transition to {target}");
        }
    }
}
