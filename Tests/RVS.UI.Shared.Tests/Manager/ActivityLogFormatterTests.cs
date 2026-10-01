using System.Globalization;
using FluentAssertions;
using RVS.Blazor.Manager.Shared;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// The manager activity log is stored as bylined blocks inside
/// <c>ServiceRequest.TechnicianSummary</c>. These cover the format itself: what a
/// comment, a system event, and a customer status note write (issue #620) look like,
/// and that each one parses back out of the log.
/// </summary>
public class ActivityLogFormatterTests
{
    private const string EmDash = "—";

    // ---- BuildBylineHeader ----------------------------------------------------

    [Fact]
    public void BuildBylineHeader_WithoutKind_ContainsAuthorAndParseableDate()
    {
        var header = ActivityLogFormatter.BuildBylineHeader("Mark Arnold");

        header.Should().StartWith("[Mark Arnold " + EmDash + " ").And.EndWith("]");
        ActivityLogFormatter.TryParseEntry($"{header}\nbody", out var entry).Should().BeTrue();
        entry!.Kind.Should().BeNull();
    }

    [Fact]
    public void BuildBylineHeader_WithKind_AppendsKindAfterTheDate()
    {
        var header = ActivityLogFormatter.BuildBylineHeader("Mark Arnold", ActivityLogFormatter.StatusNoteKind);

        header.Should().EndWith($" {EmDash} {ActivityLogFormatter.StatusNoteKind}]");
    }

    // ---- BuildStatusNoteEntry -------------------------------------------------

    [Fact]
    public void BuildStatusNoteEntry_WithNote_TagsTheKindAndCarriesTheNoteAsBody()
    {
        var entryText = ActivityLogFormatter.BuildStatusNoteEntry("Dana Ruiz", "Waiting on a slide motor, ETA Friday.");

        ActivityLogFormatter.TryParseEntry(entryText, out var entry).Should().BeTrue();
        entry!.Author.Should().Be("Dana Ruiz");
        entry.Kind.Should().Be(ActivityLogFormatter.StatusNoteKind);
        entry.Body.Should().Be("Waiting on a slide motor, ETA Friday.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildStatusNoteEntry_WithBlankNote_RecordsThatTheNoteWasCleared(string? note)
    {
        var entryText = ActivityLogFormatter.BuildStatusNoteEntry("Dana Ruiz", note);

        ActivityLogFormatter.TryParseEntry(entryText, out var entry).Should().BeTrue();
        entry!.Kind.Should().Be(ActivityLogFormatter.StatusNoteKind);
        entry.Body.Should().Be(ActivityLogFormatter.StatusNoteClearedBody);
    }

    [Fact]
    public void BuildStatusNoteEntry_WithBlankLinesInNote_StaysASingleLogBlock()
    {
        // A note is free text and may contain a blank line; blocks are separated by
        // "\n\n", so an un-collapsed note would split into an unparseable second block.
        var entryText = ActivityLogFormatter.BuildStatusNoteEntry("Dana Ruiz", "Part ordered.\n\nETA Friday.");

        entryText.Should().NotContain("\n\n");
        ActivityLogFormatter.TryParseEntry(entryText, out var entry).Should().BeTrue();
        entry!.Body.Should().Be("Part ordered.\nETA Friday.");
    }

    [Fact]
    public void BuildStatusNoteEntry_TrimsSurroundingWhitespaceFromTheNote()
    {
        var entryText = ActivityLogFormatter.BuildStatusNoteEntry("Dana Ruiz", "  Parts are in.  ");

        ActivityLogFormatter.TryParseEntry(entryText, out var entry).Should().BeTrue();
        entry!.Body.Should().Be("Parts are in.");
    }

    // ---- PrependStatusNoteEntry -----------------------------------------------

    [Fact]
    public void PrependStatusNoteEntry_WhenSummaryIsBlank_ReturnsTheEntryAlone()
    {
        var combined = ActivityLogFormatter.PrependStatusNoteEntry(null, "Dana Ruiz", "Parts are in.");

        combined.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
        ActivityLogFormatter.TryParseEntry(combined, out var entry).Should().BeTrue();
        entry!.Kind.Should().Be(ActivityLogFormatter.StatusNoteKind);
    }

    [Fact]
    public void PrependStatusNoteEntry_WithExistingSummary_PutsTheNewEntryFirst()
    {
        var existing = ActivityLogFormatter.BuildSystemEntry("Status changed to In Progress");

        var combined = ActivityLogFormatter.PrependStatusNoteEntry(existing, "Dana Ruiz", "Parts are in.");

        var blocks = combined.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        blocks.Should().HaveCount(2);
        ActivityLogFormatter.TryParseEntry(blocks[0], out var first).Should().BeTrue();
        first!.Kind.Should().Be(ActivityLogFormatter.StatusNoteKind);
        ActivityLogFormatter.TryParseEntry(blocks[1], out var second).Should().BeTrue();
        second!.Author.Should().Be(ActivityLogFormatter.SystemAuthor);
    }

    // ---- TryParseEntry --------------------------------------------------------

    [Fact]
    public void TryParseEntry_Comment_ReturnsAuthorBodyAndNoKind()
    {
        var text = $"[Mark Arnold {EmDash} Sep 23, 2026 1:02:03 PM]\nCustomer called back.";

        ActivityLogFormatter.TryParseEntry(text, out var entry).Should().BeTrue();
        entry!.Author.Should().Be("Mark Arnold");
        entry.Timestamp.Should().Be("Sep 23, 2026 1:02:03 PM");
        entry.Kind.Should().BeNull();
        entry.Body.Should().Be("Customer called back.");
    }

    [Fact]
    public void TryParseEntry_SystemEntry_ReturnsTheSystemAuthor()
    {
        var text = $"[System {EmDash} Sep 23, 2026 1:02:03 PM]\nStatus changed to In Progress";

        ActivityLogFormatter.TryParseEntry(text, out var entry).Should().BeTrue();
        entry!.Author.Should().Be(ActivityLogFormatter.SystemAuthor);
        entry.Kind.Should().BeNull();
    }

    [Fact]
    public void TryParseEntry_StatusNoteEntry_KeepsTheDateOutOfTheKind()
    {
        // Regression guard: the kind is appended with the same separator as the byline
        // date, so a lazy date group would swallow it and break the timestamp.
        var text = $"[Dana Ruiz {EmDash} Sep 23, 2026 1:02:03 PM {EmDash} {ActivityLogFormatter.StatusNoteKind}]\nParts are in.";

        ActivityLogFormatter.TryParseEntry(text, out var entry).Should().BeTrue();
        entry!.Timestamp.Should().Be("Sep 23, 2026 1:02:03 PM");
        entry.Kind.Should().Be(ActivityLogFormatter.StatusNoteKind);
        entry.Body.Should().Be("Parts are in.");
    }

    [Fact]
    public void TryParseEntry_StatusNoteEntry_TimestampRoundTripsThroughDateTime()
    {
        var text = ActivityLogFormatter.BuildStatusNoteEntry("Dana Ruiz", "Parts are in.");

        ActivityLogFormatter.TryParseEntry(text, out var entry).Should().BeTrue();
        DateTime.TryParse(entry!.Timestamp, CultureInfo.CurrentCulture, out var parsed).Should().BeTrue();
        parsed.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void TryParseEntry_MultiLineBody_KeepsEveryLine()
    {
        var text = $"[Mark Arnold {EmDash} Sep 23, 2026 1:02:03 PM]\nline one\nline two";

        ActivityLogFormatter.TryParseEntry(text, out var entry).Should().BeTrue();
        entry!.Body.Should().Be("line one\nline two");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Initial advisor notes with no byline at all.")]
    [InlineData("[Mark Arnold] no separator")]
    public void TryParseEntry_WithoutAByline_ReturnsFalse(string? block)
    {
        ActivityLogFormatter.TryParseEntry(block, out var entry).Should().BeFalse();
        entry.Should().BeNull();
    }
}
