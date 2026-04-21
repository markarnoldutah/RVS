using FluentAssertions;
using RVS.UI.Shared.Validation;

namespace RVS.UI.Shared.Tests.Validation;

public class VinTranscriptCleanerTests
{
    [Fact]
    public void Clean_WhenInputIsNull_ShouldReturnEmptyString()
    {
        var result = VinTranscriptCleaner.Clean(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Clean_WhenInputIsEmpty_ShouldReturnEmptyString()
    {
        var result = VinTranscriptCleaner.Clean("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Clean_WhenInputIsWhitespace_ShouldReturnEmptyString()
    {
        var result = VinTranscriptCleaner.Clean("   ");

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("W B A 3 A 5 C 5 9 F P 1 2 3 4 5 6", "WBA3A5C59FP123456")]
    [InlineData("w b a 3 a 5 c 5 9 f p 1 2 3 4 5 6", "WBA3A5C59FP123456")]
    public void Clean_WhenInputIsSpacedLettersAndDigits_ShouldJoinAndUppercase(string input, string expected)
    {
        var result = VinTranscriptCleaner.Clean(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Clean_WhenInputContainsSpokenNumbers_ShouldConvertToDigits()
    {
        var result = VinTranscriptCleaner.Clean("one two three four five six seven eight nine zero");

        result.Should().Be("1234567890");
    }

    [Theory]
    [InlineData("for", "4")]
    [InlineData("to", "2")]
    [InlineData("too", "2")]
    [InlineData("won", "1")]
    [InlineData("ate", "8")]
    [InlineData("eye", "I")]
    [InlineData("oh", "0")]
    public void Clean_WhenInputContainsCommonHomophones_ShouldConvertCorrectly(string input, string expected)
    {
        var result = VinTranscriptCleaner.Clean(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("alpha", "A")]
    [InlineData("bravo", "B")]
    [InlineData("charlie", "C")]
    [InlineData("delta", "D")]
    [InlineData("echo", "E")]
    [InlineData("foxtrot", "F")]
    [InlineData("golf", "G")]
    [InlineData("hotel", "H")]
    [InlineData("india", "I")]
    [InlineData("juliet", "J")]
    [InlineData("kilo", "K")]
    [InlineData("lima", "L")]
    [InlineData("mike", "M")]
    [InlineData("november", "N")]
    [InlineData("oscar", "O")]
    [InlineData("papa", "P")]
    [InlineData("quebec", "Q")]
    [InlineData("romeo", "R")]
    [InlineData("sierra", "S")]
    [InlineData("tango", "T")]
    [InlineData("uniform", "U")]
    [InlineData("victor", "V")]
    [InlineData("whiskey", "W")]
    [InlineData("x-ray", "X")]
    [InlineData("xray", "X")]
    [InlineData("yankee", "Y")]
    [InlineData("zulu", "Z")]
    public void Clean_WhenInputContainsNatoPhonetic_ShouldConvertToLetter(string input, string expected)
    {
        var result = VinTranscriptCleaner.Clean(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("banana", "B")]
    [InlineData("elephant", "E")]
    [InlineData("Mango", "M")]
    [InlineData("seventeen", "S")]
    public void Clean_WhenInputContainsUnknownWord_ShouldFallBackToFirstLetter(string input, string expected)
    {
        var result = VinTranscriptCleaner.Clean(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Clean_WhenInputMixesIrsaAndUnknownWords_ShouldApplyFirstLetterFallbackToUnknowns()
    {
        // alpha→A, banana→B (first-letter fallback), charlie→C
        var result = VinTranscriptCleaner.Clean("alpha banana charlie");

        result.Should().Be("ABC");
    }

    [Fact]
    public void Clean_WhenMultiCharTokenContainsDigits_ShouldKeepAllAlphanumericCharacters()
    {
        // Multi-char tokens with digits are treated as VIN chunks, not unknown words.
        var result = VinTranscriptCleaner.Clean("1HGBH41J");

        result.Should().Be("1HGBH41J");
    }

    [Fact]
    public void Clean_WhenInputContainsMixedWordsAndCharacters_ShouldConvertCorrectly()
    {
        var result = VinTranscriptCleaner.Clean("W B A three alpha five charlie five nine foxtrot papa one two three four five six");

        result.Should().Be("WBA3A5C59FP123456");
    }

    [Fact]
    public void Clean_WhenInputExceeds17Characters_ShouldTruncate()
    {
        var result = VinTranscriptCleaner.Clean("W B A 3 A 5 C 5 9 F P 1 2 3 4 5 6 7 8");

        result.Should().HaveLength(17);
        result.Should().Be("WBA3A5C59FP123456");
    }

    [Fact]
    public void Clean_WhenInputContainsPunctuation_ShouldStripNonAlphanumeric()
    {
        var result = VinTranscriptCleaner.Clean("W-B-A-3-A-5");

        result.Should().Be("WBA3A5");
    }

    [Fact]
    public void Clean_WhenInputContainsDashes_ShouldStripAndContinue()
    {
        var result = VinTranscriptCleaner.Clean("W.B.A.3.A.5");

        result.Should().Be("WBA3A5");
    }

    [Fact]
    public void Clean_WhenInputIsAlreadyValidVin_ShouldReturnUppercased()
    {
        var result = VinTranscriptCleaner.Clean("wba3a5c59fp123456");

        result.Should().Be("WBA3A5C59FP123456");
    }

    [Fact]
    public void Clean_WhenInputContainsDoubleAsLetterRepetition_ShouldHandleCorrectly()
    {
        var result = VinTranscriptCleaner.Clean("double U B A");

        result.Should().Be("WBA");
    }

    [Theory]
    [InlineData("I", "I")]
    [InlineData("O", "O")]
    [InlineData("Q", "Q")]
    public void Clean_WhenInputContainsSingleLetters_ShouldReturnUppercased(string input, string expected)
    {
        var result = VinTranscriptCleaner.Clean(input);

        result.Should().Be(expected);
    }
}
