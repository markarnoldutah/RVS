using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="IntakeFormStart"/> — one intake visit reaching Step 1 (<c>Spec A-13</c>,
/// issue #839). The session id is the only client-chosen key in the row, and completion rate
/// counts distinct values of it, so it has to look like something the intake app generated.
/// </summary>
public class IntakeFormStartTests
{
    [Theory]
    [InlineData("3f2b8c0e9d4a4f6b8e1c2d3a4b5c6d7e")]
    [InlineData("3f2b8c0e-9d4a-4f6b-8e1c-2d3a4b5c6d7e")]
    [InlineData("ABCDEFGH")]
    [InlineData("session_id-01")]
    public void IsWellFormedSessionId_WhenTokenLike_ShouldReturnTrue(string sessionId)
    {
        IntakeFormStart.IsWellFormedSessionId(sessionId).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("has space in it")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("quote'injection")]
    public void IsWellFormedSessionId_WhenBlankShortOrCarryingOtherCharacters_ShouldReturnFalse(string? sessionId)
    {
        IntakeFormStart.IsWellFormedSessionId(sessionId).Should().BeFalse();
    }

    [Fact]
    public void IsWellFormedSessionId_WhenLongerThanTheCap_ShouldReturnFalse()
    {
        var sessionId = new string('a', IntakeFormStart.MaxSessionIdLength + 1);

        IntakeFormStart.IsWellFormedSessionId(sessionId).Should().BeFalse();
    }

    [Fact]
    public void IsWellFormedSessionId_WhenExactlyAtTheCap_ShouldReturnTrue()
    {
        var sessionId = new string('a', IntakeFormStart.MaxSessionIdLength);

        IntakeFormStart.IsWellFormedSessionId(sessionId).Should().BeTrue();
    }
}
