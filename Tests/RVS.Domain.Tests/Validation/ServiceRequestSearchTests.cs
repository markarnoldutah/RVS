using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="ServiceRequestSearch"/> — the fixed vocabulary of the service-request
/// search (issue #849): which scopes exist, the list's row cap and the Open status group.
/// </summary>
public class ServiceRequestSearchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("List")]
    [InlineData("Board")]
    public void IsValidScope_WhenBlankOrKnown_ShouldBeTrue(string? scope)
    {
        ServiceRequestSearch.IsValidScope(scope).Should().BeTrue();
    }

    [Theory]
    [InlineData("board")]
    [InlineData("All")]
    [InlineData("Board; DROP")]
    public void IsValidScope_WhenUnknown_ShouldBeFalse(string scope)
    {
        ServiceRequestSearch.IsValidScope(scope).Should().BeFalse();
    }

    [Theory]
    [InlineData("Board", true)]
    [InlineData("List", false)]
    [InlineData(null, false)]
    public void IsBoardScope_ShouldMatchOnlyTheBoardScope(string? scope, bool expected)
    {
        ServiceRequestSearch.IsBoardScope(scope).Should().Be(expected);
    }

    [Fact]
    public void MaxListResults_ShouldBeFiveHundred()
    {
        ServiceRequestSearch.MaxListResults.Should().Be(500);
    }

    [Fact]
    public void OpenStatusFilter_ShouldNotCollideWithAStoredStatus()
    {
        var storedStatuses = StatusTransitions.GetAllowedTargets("New").Append("New");

        storedStatuses.Should().NotContain(ServiceRequestSearch.OpenStatusFilter);
    }
}
