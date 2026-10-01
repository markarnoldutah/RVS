using FluentAssertions;
using RVS.Domain.Packets;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketReceivedFormatter"/> — the one place the packet's Received line
/// is composed, for both renderers (issue #506, finishing #492 item 4).
/// </summary>
public class PacketReceivedFormatterTests
{
    /// <summary>2026-09-05 14:30 UTC — the instant every packet fixture in the suite uses.</summary>
    private static readonly DateTimeOffset September = new(2026, 9, 5, 14, 30, 0, TimeSpan.Zero);

    /// <summary>2026-01-15 14:30 UTC — a standard-time instant for the same zones.</summary>
    private static readonly DateTimeOffset January = new(2026, 1, 15, 14, 30, 0, TimeSpan.Zero);

    public static TheoryData<string> CuratedIds()
    {
        var data = new TheoryData<string>();
        foreach (var zone in DealershipTimeZones.All)
        {
            data.Add(zone.IanaId);
        }

        return data;
    }

    // ── Fallback: the pre-#506 string, unchanged ─────────────────────────

    [Fact]
    public void Format_WhenTimeZoneIdIsNull_ShouldReturnTodaysUtcString()
    {
        PacketReceivedFormatter.Format(September, null)
            .Should().Be("2026-09-05 2:30 PM UTC");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_WhenTimeZoneIdIsBlank_ShouldReturnTheUtcString(string timeZoneId)
    {
        PacketReceivedFormatter.Format(September, timeZoneId)
            .Should().Be("2026-09-05 2:30 PM UTC");
    }

    [Fact]
    public void Format_WhenTimeZoneIdIsUnresolvable_ShouldReturnTheUtcString()
    {
        PacketReceivedFormatter.Format(September, "Mars/Olympus_Mons")
            .Should().Be("2026-09-05 2:30 PM UTC");
    }

    [Fact]
    public void FormatUtc_ShouldRenderTheInvariantUtcForm()
    {
        PacketReceivedFormatter.FormatUtc(September).Should().Be("2026-09-05 2:30 PM UTC");
    }

    // ── 12-hour clock with an AM/PM suffix (issue #735) ──────────────────

    [Theory]
    [InlineData(0, 5, "2026-09-05 12:05 AM UTC")]
    [InlineData(9, 0, "2026-09-05 9:00 AM UTC")]
    [InlineData(12, 0, "2026-09-05 12:00 PM UTC")]
    [InlineData(23, 59, "2026-09-05 11:59 PM UTC")]
    public void FormatUtc_ShouldUseATwelveHourClockWithAnAmPmSuffix(int hour, int minute, string expected)
    {
        var instant = new DateTimeOffset(2026, 9, 5, hour, minute, 0, TimeSpan.Zero);

        PacketReceivedFormatter.FormatUtc(instant).Should().Be(expected);
    }

    // ── Curated zones: abbreviation from our own table ───────────────────

    [Fact]
    public void Format_ForAmericaDenverInSeptember_ShouldRenderMountainDaylightTime()
    {
        PacketReceivedFormatter.Format(September, "America/Denver")
            .Should().Be("2026-09-05 8:30 AM MDT");
    }

    [Fact]
    public void Format_ForAmericaDenverInJanuary_ShouldRenderMountainStandardTime()
    {
        PacketReceivedFormatter.Format(January, "America/Denver")
            .Should().Be("2026-01-15 7:30 AM MST");
    }

    [Fact]
    public void Format_ForAmericaPhoenixInSeptember_ShouldRenderMst()
    {
        // Arizona does not observe DST, so the offset and the spelling never move.
        PacketReceivedFormatter.Format(September, "America/Phoenix")
            .Should().Be("2026-09-05 7:30 AM MST");
    }

    [Fact]
    public void Format_ForAmericaPhoenixInJanuary_ShouldAlsoRenderMst()
    {
        PacketReceivedFormatter.Format(January, "America/Phoenix")
            .Should().Be("2026-01-15 7:30 AM MST");
    }

    [Fact]
    public void Format_ForPacificHonolulu_ShouldRenderHst()
    {
        PacketReceivedFormatter.Format(September, "Pacific/Honolulu")
            .Should().Be("2026-09-05 4:30 AM HST");
    }

    [Fact]
    public void Format_ForAmericaAnchorageInSeptember_ShouldRenderAkdt()
    {
        PacketReceivedFormatter.Format(September, "America/Anchorage")
            .Should().Be("2026-09-05 6:30 AM AKDT");
    }

    [Fact]
    public void Format_ForAmericaNewYorkInSeptember_ShouldRenderEdt()
    {
        PacketReceivedFormatter.Format(September, "America/New_York")
            .Should().Be("2026-09-05 10:30 AM EDT");
    }

    [Fact]
    public void Format_ShouldMatchTheIdCaseInsensitively()
    {
        PacketReceivedFormatter.Format(September, "america/denver")
            .Should().Be("2026-09-05 8:30 AM MDT");
    }

    // ── Resolvable but uncurated: numeric offset ─────────────────────────

    [Fact]
    public void Format_ForAnUncuratedButResolvableZone_ShouldRenderANumericOffset()
    {
        PacketReceivedFormatter.Format(September, "Europe/Berlin")
            .Should().Be("2026-09-05 4:30 PM (UTC+02:00)");
    }

    [Fact]
    public void Format_ForAnUncuratedZoneWithAHalfHourOffset_ShouldRenderTheMinutes()
    {
        PacketReceivedFormatter.Format(September, "Asia/Kolkata")
            .Should().Be("2026-09-05 8:00 PM (UTC+05:30)");
    }

    [Fact]
    public void Format_ForAnUncuratedZoneAtZeroOffset_ShouldRenderPlusZero()
    {
        PacketReceivedFormatter.Format(January, "Europe/London")
            .Should().Be("2026-01-15 2:30 PM (UTC+00:00)");
    }

    // ── Instant handling ─────────────────────────────────────────────────

    [Fact]
    public void Format_WhenTheInstantCarriesANonZeroOffset_ShouldConvertFromTheInstant()
    {
        // 09:30-05:00 is the same instant as 14:30Z, so Denver still reads 08:30 MDT.
        var sameInstant = new DateTimeOffset(2026, 9, 5, 9, 30, 0, TimeSpan.FromHours(-5));

        PacketReceivedFormatter.Format(sameInstant, "America/Denver")
            .Should().Be("2026-09-05 8:30 AM MDT");
    }

    [Fact]
    public void Format_WhenTheInstantCarriesANonZeroOffset_ShouldStillNormaliseTheUtcFallback()
    {
        var sameInstant = new DateTimeOffset(2026, 9, 5, 9, 30, 0, TimeSpan.FromHours(-5));

        PacketReceivedFormatter.Format(sameInstant, null)
            .Should().Be("2026-09-05 2:30 PM UTC");
    }

    [Fact]
    public void Format_AcrossADaylightSavingBoundary_ShouldPickTheAbbreviationForThatInstant()
    {
        // Mountain DST ended 02:00 MDT on Sunday 2026-11-01 — 08:00 UTC — when the clock fell
        // back to 01:00 MST. The two instants either side land on the same wall clock and can
        // only be told apart by the abbreviation, which is exactly why we key off the instant.
        var beforeFallBack = new DateTimeOffset(2026, 11, 1, 7, 30, 0, TimeSpan.Zero);
        var afterFallBack = new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.Zero);

        PacketReceivedFormatter.Format(beforeFallBack, "America/Denver")
            .Should().Be("2026-11-01 1:30 AM MDT");
        PacketReceivedFormatter.Format(afterFallBack, "America/Denver")
            .Should().Be("2026-11-01 1:30 AM MST");
    }

    // ── The CSS running-footer guard ─────────────────────────────────────

    [Theory]
    [MemberData(nameof(CuratedIds))]
    public void Format_ForEveryCuratedZone_ShouldEmitOnlyCssSafeAsciiCharacters(string ianaId)
    {
        // PacketHtmlRenderer interpolates this string into a CSS content: literal, which must
        // be ASCII and carry no quote or backslash. The abbreviation comes from our own table
        // precisely so this holds; assert it rather than assume it.
        foreach (var instant in new[] { September, January })
        {
            var received = PacketReceivedFormatter.Format(instant, ianaId);

            received.Should().NotContain("\"").And.NotContain("\\");
            received.ToCharArray().Should().OnlyContain(ch => ch >= ' ' && ch < (char)127);
        }
    }

    [Fact]
    public void Format_ForAnUncuratedZone_ShouldAlsoEmitOnlyCssSafeAsciiCharacters()
    {
        var received = PacketReceivedFormatter.Format(September, "Europe/Berlin");

        received.Should().NotContain("\"").And.NotContain("\\");
        received.ToCharArray().Should().OnlyContain(ch => ch >= ' ' && ch < (char)127);
    }
}
