using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class VehicleHistoryKeyTests
{
    // ── NormalizeManufacturer ────────────────────────────────────────────────

    [Theory]
    [InlineData("Lance", "LANCE")]
    [InlineData("Lance Camper", "LANCE")]
    [InlineData("LANCE CAMPER MFG. CORP", "LANCE")]
    [InlineData("Lance Camper Mfg. Corp.", "LANCE")]
    [InlineData("  lance  ", "LANCE")]
    [InlineData("Lance Campers, Inc.", "LANCE")]
    [InlineData("Northern Lite Manufacturing Ltd", "NORTHERNLITE")]
    [InlineData("Northern-Lite", "NORTHERNLITE")]
    [InlineData("Forest River", "FORESTRIVER")]
    [InlineData("Forest River, Inc.", "FORESTRIVER")]
    [InlineData("ForestRiver", "FORESTRIVER")]
    [InlineData("Arctic Fox Trailers LLC", "ARCTICFOX")]
    [InlineData("The Cirrus Company", "CIRRUS")]
    [InlineData("Host Industries", "HOST")]
    public void NormalizeManufacturer_ShouldCollapseSpellingsOfOneManufacturer(string input, string expected)
    {
        VehicleHistoryKey.NormalizeManufacturer(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Thor Motor Coach", "THORMOTORCOACH")]
    [InlineData("Entegra Coach", "ENTEGRACOACH")]
    public void NormalizeManufacturer_ShouldKeepWordsThatDistinguishOneMakerFromAnother(string input, string expected)
    {
        // "Coach" and "Motor" are part of a brand, not a corporate suffix: stripping them would
        // merge Thor Motor Coach with Thor Industries. A split history beats a false merge.
        VehicleHistoryKey.NormalizeManufacturer(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeManufacturer_ShouldStripSuffixesOnlyFromTheEnd()
    {
        VehicleHistoryKey.NormalizeManufacturer("Camper Company of America").Should().Be("CAMPERCOMPANYOFAMERICA");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("Inc.")]
    [InlineData("Camper")]
    [InlineData("RV Mfg Co")]
    [InlineData("The")]
    public void NormalizeManufacturer_WhenNothingIdentifyingIsLeft_ShouldReturnEmpty(string? input)
    {
        VehicleHistoryKey.NormalizeManufacturer(input).Should().BeEmpty();
    }

    [Fact]
    public void NormalizeManufacturer_ShouldKeepOnlyAsciiLettersAndDigits()
    {
        VehicleHistoryKey.NormalizeManufacturer("Lance: <script>").Should().Be("LANCESCRIPT");
    }

    // ── For ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1HGBH41JXMN109186", "Grand Design")]
    [InlineData("1HGBH41JXMN109186", null)]
    [InlineData(" 1hgbh41jxmn109186 ", "")]
    public void For_WhenVin_ShouldReturnTheNormalisedVinWhateverTheManufacturer(string identifier, string? manufacturer)
    {
        VehicleHistoryKey.For(identifier, manufacturer).Should().Be("1HGBH41JXMN109186");
    }

    [Theory]
    [InlineData("152263", "Lance", "LANCE:152263")]
    [InlineData(" 152 263 ", "LANCE CAMPER MFG. CORP", "LANCE:152263")]
    [InlineData("ab-1234", "Northern Lite", "NORTHERNLITE:AB-1234")]
    public void For_WhenSerialNumberWithManufacturer_ShouldReturnTheCompositeKey(string identifier, string manufacturer, string expected)
    {
        VehicleHistoryKey.For(identifier, manufacturer).Should().Be(expected);
    }

    [Fact]
    public void For_WhenSameSerialFromTwoManufacturers_ShouldReturnDifferentKeys()
    {
        var lance = VehicleHistoryKey.For("152263", "Lance");
        var northernLite = VehicleHistoryKey.For("152263", "Northern Lite");

        lance.Should().NotBe(northernLite);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Camper Inc.")]
    public void For_WhenSerialNumberWithoutManufacturer_ShouldReturnNull(string? manufacturer)
    {
        VehicleHistoryKey.For("152263", manufacturer).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234<5")]
    [InlineData("ABCDEF")]
    [InlineData("1HGBH41JXMN10918O")]
    public void For_WhenIdentifierIsBlankOrInvalid_ShouldReturnNull(string? identifier)
    {
        VehicleHistoryKey.For(identifier, "Lance").Should().BeNull();
    }

    // ── ToIdentifier ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("LANCE:152263", "152263")]
    [InlineData("NORTHERNLITE:AB-1234", "AB-1234")]
    [InlineData("1HGBH41JXMN109186", "1HGBH41JXMN109186")]
    [InlineData("", "")]
    public void ToIdentifier_ShouldReturnTheVinOrSerialNumberAsTheCustomerGaveIt(string key, string expected)
    {
        VehicleHistoryKey.ToIdentifier(key).Should().Be(expected);
    }

    [Fact]
    public void ToIdentifier_ShouldRoundTripACompositeKey()
    {
        var key = VehicleHistoryKey.For("152263", "Lance")!;

        VehicleHistoryKey.ToIdentifier(key).Should().Be("152263");
    }
}
