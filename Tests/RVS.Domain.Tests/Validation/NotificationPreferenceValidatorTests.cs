using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="NotificationPreferenceValidator"/> — the opt-outs are a hard veto over the
/// preferred contact method (<c>Spec A-2</c>, issue #662).
/// </summary>
public class NotificationPreferenceValidatorTests
{
    // ── IsContactMethodAvailable ─────────────────────────────────────────

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IsContactMethodAvailable_Phone_ShouldAlwaysBeAvailable(bool smsOptOut, bool emailOptOut)
    {
        NotificationPreferenceValidator.IsContactMethodAvailable("Phone", smsOptOut, emailOptOut).Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsContactMethodAvailable_Text_ShouldBeAvailableOnlyWithoutSmsOptOut(bool smsOptOut, bool expected)
    {
        NotificationPreferenceValidator.IsContactMethodAvailable("Text", smsOptOut, false).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsContactMethodAvailable_Email_ShouldBeAvailableOnlyWithoutEmailOptOut(bool emailOptOut, bool expected)
    {
        NotificationPreferenceValidator.IsContactMethodAvailable("Email", false, emailOptOut).Should().Be(expected);
    }

    [Fact]
    public void IsContactMethodAvailable_ShouldMatchCaseInsensitivelyAndTrim()
    {
        NotificationPreferenceValidator.IsContactMethodAvailable("  text ", smsOptOut: true, emailOptOut: false).Should().BeFalse();
        NotificationPreferenceValidator.IsContactMethodAvailable("EMAIL", smsOptOut: false, emailOptOut: true).Should().BeFalse();
    }

    [Fact]
    public void IsContactMethodAvailable_WhenSmsOptOut_ShouldNotAffectEmail()
    {
        NotificationPreferenceValidator.IsContactMethodAvailable("Email", smsOptOut: true, emailOptOut: false).Should().BeTrue();
    }

    [Fact]
    public void IsContactMethodAvailable_WhenEmailOptOut_ShouldNotAffectText()
    {
        NotificationPreferenceValidator.IsContactMethodAvailable("Text", smsOptOut: false, emailOptOut: true).Should().BeTrue();
    }

    // ── Validate ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_TextWithSmsOptOut_ShouldFail()
    {
        var result = NotificationPreferenceValidator.Validate("Text", smsOptOut: true, emailOptOut: false);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Text");
    }

    [Fact]
    public void Validate_EmailWithEmailOptOut_ShouldFail()
    {
        var result = NotificationPreferenceValidator.Validate("Email", smsOptOut: false, emailOptOut: true);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Email");
    }

    [Fact]
    public void Validate_ConflictMatchedCaseInsensitively_ShouldFail()
    {
        NotificationPreferenceValidator.Validate(" text ", smsOptOut: true, emailOptOut: false).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Phone", true, true)]
    [InlineData("Text", false, true)]
    [InlineData("Email", true, false)]
    [InlineData("Text", false, false)]
    [InlineData("Email", false, false)]
    public void Validate_NoConflict_ShouldSucceed(string preferredContact, bool smsOptOut, bool emailOptOut)
    {
        NotificationPreferenceValidator.Validate(preferredContact, smsOptOut, emailOptOut).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenPreferenceBlank_ShouldSucceed(string? preferredContact)
    {
        // Required-ness is the wizard's rule; the stored value is optional (pre-#472 requests).
        NotificationPreferenceValidator.Validate(preferredContact, smsOptOut: true, emailOptOut: true).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenPreferenceUnknown_ShouldSucceed()
    {
        // Vocabulary is PreferredContactMethod.Normalize's job; this validator only checks the veto.
        NotificationPreferenceValidator.Validate("Fax", smsOptOut: true, emailOptOut: true).IsValid.Should().BeTrue();
    }
}
