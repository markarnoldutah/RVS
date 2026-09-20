using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="SmsKeywordVocabulary"/> — the carrier keywords the inbound handler
/// acts on (<c>Spec A-2</c> out-of-scope note, issue #665). Everything that is not an exact
/// keyword is ignored: there is no inbox and no conversation.
/// </summary>
public class SmsKeywordVocabularyTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("STOPALL")]
    [InlineData("UNSUBSCRIBE")]
    [InlineData("CANCEL")]
    [InlineData("END")]
    [InlineData("QUIT")]
    public void Classify_WhenOptOutKeyword_ShouldBeOptOut(string message)
    {
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.OptOut);
    }

    [Theory]
    [InlineData("START")]
    [InlineData("UNSTOP")]
    public void Classify_WhenOptInKeyword_ShouldBeOptIn(string message)
    {
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.OptIn);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("Stop")]
    [InlineData("  stop  ")]
    [InlineData("stop.")]
    [InlineData("STOP!")]
    [InlineData("\"STOP\"")]
    public void Classify_ShouldIgnoreCaseSurroundingSpaceAndPunctuation(string message)
    {
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.OptOut);
    }

    [Theory]
    [InlineData("STOP ALL")]
    [InlineData("stop all")]
    public void Classify_WhenKeywordIsSpacedInternally_ShouldStillMatch(string message)
    {
        // Carriers publish STOPALL; people type "STOP ALL".
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.OptOut);
    }

    [Theory]
    [InlineData("HELP")]
    [InlineData("help")]
    [InlineData(" Help! ")]
    [InlineData("INFO")]
    public void Classify_WhenHelp_ShouldBeHelp(string message)
    {
        // HELP is answered with one fixed reply (Plan, Sep 19 2026). INFO is the synonym the
        // carriers publish alongside it.
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.Help);
    }

    [Theory]
    [InlineData("please stop texting me")]
    [InlineData("when can I start my repair?")]
    [InlineData("Thanks!")]
    [InlineData("STOPPED")]
    [InlineData("restart")]
    [InlineData("helpful")]
    [InlineData("can you help me")]
    public void Classify_WhenNotAnExactKeyword_ShouldBeNone(string message)
    {
        // Only exact keywords count, matching what the carrier itself acts on. A sentence that
        // merely contains "stop" is a conversation we do not have.
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_WhenNullOrBlank_ShouldBeNone(string? message)
    {
        SmsKeywordVocabulary.Classify(message).Should().Be(SmsKeyword.None);
    }

    [Fact]
    public void Classify_WhenAbsurdlyLong_ShouldBeNoneWithoutScanningItAll()
    {
        SmsKeywordVocabulary.Classify(new string('x', 5000)).Should().Be(SmsKeyword.None);
    }
}
