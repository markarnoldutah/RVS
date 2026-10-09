using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="JobTypes"/> — how the operator will do the work (<c>Spec C-11</c>,
/// issue #843). One fixed set for every location; not the A-5 issue category.
/// </summary>
public class JobTypesTests
{
    [Fact]
    public void Codes_ShouldBeTheSixC11ValuesInSpecOrder()
    {
        JobTypes.Codes
            .Should().Equal("Remote", "OnSite", "InShop", "Inspection", "Install", "Other");
    }

    [Fact]
    public void All_ShouldBeInAscendingSortOrder()
    {
        JobTypes.All.Select(e => e.SortOrder).Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData("Remote")]
    [InlineData("OnSite")]
    [InlineData("InShop")]
    [InlineData("Inspection")]
    [InlineData("Install")]
    [InlineData("Other")]
    public void IsValid_WhenKnownCode_ShouldReturnTrue(string code)
    {
        JobTypes.IsValid(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("remote")]
    [InlineData("On-site")]
    [InlineData("Solar")]
    [InlineData("NotTriaged")]
    public void IsValid_WhenUnknownOrBlankOrMiscased_ShouldReturnFalse(string? code)
    {
        JobTypes.IsValid(code).Should().BeFalse();
    }

    [Theory]
    [InlineData("Remote", "Remote support")]
    [InlineData("OnSite", "On-site repair")]
    [InlineData("InShop", "In-shop repair")]
    [InlineData("Inspection", "Inspection")]
    [InlineData("Install", "Install / upgrade")]
    [InlineData("Other", "Other")]
    public void GetLabel_WhenKnownCode_ShouldReturnHumanLabel(string code, string expected)
    {
        JobTypes.GetLabel(code).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetLabel_WhenUnset_ShouldReturnNotTriaged(string? code)
    {
        JobTypes.GetLabel(code).Should().Be("Not triaged");
    }

    [Fact]
    public void GetLabel_WhenUnknownCode_ShouldReturnTheRawValue()
    {
        JobTypes.GetLabel("SomethingNew").Should().Be("SomethingNew");
    }

    [Theory]
    [InlineData("Remote")]
    [InlineData("Other")]
    [InlineData("NotTriaged")]
    public void IsValidFilter_WhenCodeOrNotTriaged_ShouldReturnTrue(string filter)
    {
        JobTypes.IsValidFilter(filter).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Solar")]
    [InlineData("nottriaged")]
    public void IsValidFilter_WhenBlankOrUnknown_ShouldReturnFalse(string? filter)
    {
        JobTypes.IsValidFilter(filter).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Remote", null)]
    [InlineData("Remote", "")]
    [InlineData(null, "NotTriaged")]
    [InlineData("", "NotTriaged")]
    [InlineData("OnSite", "OnSite")]
    public void MatchesFilter_ShouldMatch(string? jobType, string? filter)
    {
        JobTypes.MatchesFilter(jobType, filter).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "Remote")]
    [InlineData("Remote", "NotTriaged")]
    [InlineData("Remote", "OnSite")]
    public void MatchesFilter_ShouldNotMatch(string? jobType, string? filter)
    {
        JobTypes.MatchesFilter(jobType, filter).Should().BeFalse();
    }
}
