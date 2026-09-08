using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="PreferredContactMethod"/> — the controlled vocabulary for the
/// customer's preferred contact method on the packet (<c>Spec A-2</c>, <c>Spec B-2</c> item 2).
/// </summary>
public class PreferredContactMethodTests
{
    [Fact]
    public void AllowedValues_ShouldBePhoneTextEmailInDisplayOrder()
    {
        PreferredContactMethod.AllowedValues.Should().Equal("Phone", "Text", "Email");
    }

    [Theory]
    [InlineData("Phone")]
    [InlineData("Text")]
    [InlineData("Email")]
    public void Normalize_WhenCanonicalValue_ShouldReturnItUnchanged(string value)
    {
        PreferredContactMethod.Normalize(value).Should().Be(value);
    }

    [Theory]
    [InlineData("phone", "Phone")]
    [InlineData("  TEXT  ", "Text")]
    [InlineData("eMaIl", "Email")]
    public void Normalize_ShouldMatchCaseInsensitivelyAndTrim(string value, string expected)
    {
        PreferredContactMethod.Normalize(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WhenBlank_ShouldReturnNull(string? value)
    {
        PreferredContactMethod.Normalize(value).Should().BeNull();
    }

    [Theory]
    [InlineData("Fax")]
    [InlineData("Carrier Pigeon")]
    [InlineData("SMS")]
    public void Normalize_WhenUnknownValue_ShouldThrowArgumentException(string value)
    {
        var act = () => PreferredContactMethod.Normalize(value);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Phone", true)]
    [InlineData("text", true)]
    [InlineData("  Email ", true)]
    [InlineData("Fax", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValid_ShouldReflectMembershipInAllowedValues(string? value, bool expected)
    {
        PreferredContactMethod.IsValid(value).Should().Be(expected);
    }
}
