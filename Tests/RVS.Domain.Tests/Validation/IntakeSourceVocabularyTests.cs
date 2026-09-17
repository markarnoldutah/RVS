using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="IntakeSourceVocabulary"/> — the controlled vocabulary for the
/// distribution channel a customer arrived through (<c>Spec A-13</c>, issue #599).
/// </summary>
public class IntakeSourceVocabularyTests
{
    [Fact]
    public void KnownValues_ShouldBeTheFourChannelsInTableOrder()
    {
        IntakeSourceVocabulary.KnownValues
            .Should().Equal("textrepl", "quickreply", "qr", "print");
    }

    [Theory]
    [InlineData("textrepl")]
    [InlineData("quickreply")]
    [InlineData("qr")]
    [InlineData("print")]
    public void Normalize_WhenKnownValue_ShouldReturnItUnchanged(string value)
    {
        IntakeSourceVocabulary.Normalize(value).Should().Be(value);
    }

    [Theory]
    [InlineData("QR", "qr")]
    [InlineData("  TextRepl  ", "textrepl")]
    [InlineData("QuickReply", "quickreply")]
    public void Normalize_ShouldLowercaseAndTrim(string value, string expected)
    {
        IntakeSourceVocabulary.Normalize(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WhenAbsent_ShouldDefaultToPrint(string? value)
    {
        // Printed material (business cards, invoices) cannot carry a query string,
        // so "no src" is the print channel rather than an unknown one.
        IntakeSourceVocabulary.Normalize(value).Should().Be(IntakeSourceVocabulary.Print);
    }

    [Theory]
    [InlineData("nfc")]
    [InlineData("radio-spot")]
    [InlineData("campaign_42")]
    public void Normalize_WhenUnknownButWellFormed_ShouldKeepTheValue(string value)
    {
        // Unknown values must be accepted so the redirect never fails on one (Spec A-13).
        IntakeSourceVocabulary.Normalize(value).Should().Be(value);
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("a b")]
    [InlineData("drop;table")]
    [InlineData("-leading-hyphen")]
    [InlineData("emoji✨")]
    public void Normalize_WhenMalformed_ShouldCoerceToOther(string value)
    {
        IntakeSourceVocabulary.Normalize(value).Should().Be(IntakeSourceVocabulary.Other);
    }

    [Fact]
    public void Normalize_WhenLongerThanMaxLength_ShouldCoerceToOther()
    {
        var tooLong = new string('a', IntakeSourceVocabulary.MaxLength + 1);

        IntakeSourceVocabulary.Normalize(tooLong).Should().Be(IntakeSourceVocabulary.Other);
    }

    [Fact]
    public void Normalize_WhenExactlyMaxLength_ShouldKeepTheValue()
    {
        var atLimit = new string('a', IntakeSourceVocabulary.MaxLength);

        IntakeSourceVocabulary.Normalize(atLimit).Should().Be(atLimit);
    }

    [Theory]
    [InlineData("qr", true)]
    [InlineData("QR", true)]
    [InlineData("nfc", false)]
    [InlineData(null, false)]
    public void IsKnown_ShouldRecogniseOnlyTheTabledChannels(string? value, bool expected)
    {
        IntakeSourceVocabulary.IsKnown(value).Should().Be(expected);
    }
}
