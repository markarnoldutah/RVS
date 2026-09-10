using System.Text.RegularExpressions;
using FluentAssertions;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketHtmlRenderer"/> — the pure transform that renders a
/// <see cref="ServicePacket"/> to a self-contained HTML document with an embedded print
/// stylesheet (<c>Spec B-3</c>, issue <c>#431</c>).
///
/// The renderer does presentation only: it reads the composed <see cref="ServicePacket"/>
/// in <c>Spec B-2</c> order and never inspects a <c>ServiceRequest</c>. Greyscale
/// legibility is asserted structurally — every semantic block carries a textual label and
/// a border/weight treatment, never colour alone.
/// </summary>
public class PacketHtmlRendererTests
{
    // ── Fixtures ────────────────────────────────────────────────────────────

    private static ServicePacket FullPacket() => new()
    {
        Unit = new PacketUnitHeader
        {
            Year = 2021,
            Make = "Winnebago",
            Model = "View",
            Vin = "1FDXE45S12HB00001",
        },
        Customer = new PacketCustomer
        {
            FullName = "Dale Gribble",
            FirstName = "Dale",
            LastName = "Gribble",
            Phone = "555-0101",
            Email = "dale@example.com",
            PreferredContact = "Phone",
        },
        Origin = new PacketOrigin
        {
            LocationName = "Salt Lake Service Center",
            LocationPhone = "555-0199",
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
            ReferenceCode = "A1B2C3D4",
        },
        IssueCategory = "Electrical",
        IssueDescription = "Generator quits after ten minutes. Smells hot.",
        Diagnostics =
        [
            new PacketDiagnosticEntry
            {
                Question = "Does the generator start at all?",
                Answers = ["Yes, then dies", "Dies after about 10 minutes"],
            },
            new PacketDiagnosticEntry
            {
                Question = "Any warning lights?",
                Answers = ["Temp light"],
            },
        ],
        AiSummary = new PacketAiSummary { Text = "Likely overheating on the generator windings." },
        Photos =
        [
            new PacketPhoto { Url = "https://blob/generator.jpg?sas=read", FileName = "generator.jpg" },
        ],
        PasteBlock = "ELECTRICAL\nGenerator quits after ten minutes.\nhttps://rvintake.com/status/abc123",
        StatusLink = new PacketStatusLink { Url = "https://rvintake.com/status/abc123" },
    };

    private static ServicePacket MinimalPacket() => new()
    {
        Unit = new PacketUnitHeader(),
        Customer = new PacketCustomer { FullName = "Jane Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
            ReferenceCode = "DEADBEEF",
        },
        IssueDescription = "It rattles.",
        Diagnostics = [],
        Photos = [],
    };

    private static int Order(string html, string marker)
    {
        var index = html.IndexOf(marker, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "marker '{0}' should be present", marker);
        return index;
    }

    // ── Guard clause ────────────────────────────────────────────────────────

    [Fact]
    public void Render_WhenPacketIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketHtmlRenderer.Render(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Document shape ──────────────────────────────────────────────────────

    [Fact]
    public void Render_ShouldProduceAWellFormedHtmlDocument()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html").And.Contain("</html>");
        html.Should().Contain("<meta charset=\"utf-8\"");
    }

    [Fact]
    public void Render_ShouldBeSelfContained_WithNoExternalResourcesOrScripts()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().NotContain("<link", "the stylesheet must be embedded, not linked");
        html.Should().NotContain("<script", "the packet is a static document");
        html.Should().Contain("<style", "the print stylesheet is embedded inline");
    }

    [Fact]
    public void Render_ShouldEmbedAPrintStylesheet()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("@media print");
        html.Should().MatchRegex(@"@page\s*\{[^}]*margin");
    }

    [Fact]
    public void Render_ShouldNotPinPaperSize_SoLetterAndA4BothPrintCleanly()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        // A fixed `size: letter` / `size: A4` in @page forces one paper and clips the
        // other. Margins are declared; the paper is left to the printer.
        Regex.IsMatch(html, @"@page\s*\{[^}]*\bsize\s*:\s*(letter|a4)", RegexOptions.IgnoreCase)
            .Should().BeFalse();
    }

    // ── Section ordering (Spec B-2) ─────────────────────────────────────────

    [Fact]
    public void Render_ShouldEmitEverySectionInSpecB2Order()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        // Spec B-2 order, with the AI assessment lifted above the complaint.
        var order = new[]
        {
            Order(html, "section:unit"),
            Order(html, "section:customer"),
            Order(html, "section:origin"),
            Order(html, "section:category"),
            Order(html, "section:ai-summary"),
            Order(html, "section:description"),
            Order(html, "section:diagnostics"),
            Order(html, "section:photos"),
            Order(html, "section:paste-block"),
            Order(html, "section:status-link"),
        };

        order.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Render_ShouldPlaceThePreliminaryAssessmentAboveTheComplaint()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        Order(html, "section:ai-summary").Should().BeLessThan(Order(html, "section:description"));
        Order(html, "section:category").Should().BeLessThan(Order(html, "section:ai-summary"));
    }

    // ── 1. Unit header ─────────────────────────────────────────────────────

    [Fact]
    public void Render_WhenVinPresent_ShouldRenderTheSerialLine()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        // IDS calls the VIN the "Serial#"; we label it "Serial# (VIN)".
        html.Should().Contain("Serial# (VIN):").And.Contain("1FDXE45S12HB00001");
        html.Should().Contain("2021").And.Contain("Winnebago").And.Contain("View");
    }

    [Fact]
    public void Render_WhenVinAbsent_ShouldOmitTheSerialLine()
    {
        var packet = FullPacket() with { Unit = new PacketUnitHeader { Year = 2021, Make = "Winnebago", Model = "View" } };

        var html = PacketHtmlRenderer.Render(packet);

        html.Should().NotContain("Serial#");
        html.Should().NotContain("1FDXE45S12HB00001");
    }

    [Fact]
    public void Render_WhenYearMakeModelAllAbsent_ShouldRenderAFallbackAndNotCrash()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        Order(html, "section:unit");
        html.Should().Contain("Unit details not provided");
    }

    // ── 2. Customer ────────────────────────────────────────────────────────

    [Fact]
    public void Render_ShouldRenderCustomerContactRows_AndSkipAbsentOnes()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        html.Should().Contain("Jane Doe");
        // MinimalPacket has no phone/email/preferred-contact — those labels must not appear.
        var customerBlock = html[Order(html, "section:customer")..Order(html, "section:origin")];
        customerBlock.Should().NotContain("Phone:");
        customerBlock.Should().NotContain("Email:");
        customerBlock.Should().NotContain("Preferred contact:");
    }

    [Fact]
    public void Render_ShouldRenderPreferredContact_WhenPresent()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("Preferred contact:").And.Contain("Phone");
    }

    // ── 3. Origin ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_ShouldRenderReferenceCodeAndAnInvariantTimestamp()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("A1B2C3D4");
        html.Should().Contain("2026-09-05 14:30 UTC");
        html.Should().Contain("Salt Lake Service Center");
    }

    // ── IDS work-order alignment (issue #431, Blue Compass / Integrated Dealer Systems) ──

    [Fact]
    public void Render_ShouldPlaceTheReferenceCodeInTheMastheadAsRvsNumber()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("RVS #:");
        // Mirrors IDS "W/O #" top-right: the tracking number comes before any section body.
        html.IndexOf("A1B2C3D4", StringComparison.Ordinal)
            .Should().BeLessThan(Order(html, "section:customer"));
        // The top-of-packet Received line carries the full timestamp, date + time, UTC
        // (issue #492 item 4).
        html.Should().Contain("Received: 2026-09-05 14:30 UTC");
    }

    // ── Masthead: brand, logo, customer headline, received line (issue #492) ──

    [Fact]
    public void Render_ByDefault_ShouldTitleTheMastheadWithTheProductBrand()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        var masthead = html[..Order(html, "section:customer")];
        masthead.Should().Contain("RV Intake");
        html.Should().NotContain("RV ServiceFlow");
    }

    [Fact]
    public void Render_WhenBrandNameOverridden_ShouldUseItInTheMastheadAndBothFooters()
    {
        var packet = FullPacket() with
        {
            Branding = new PacketBranding { BrandName = "Acme RV Group" },
        };

        var html = PacketHtmlRenderer.Render(packet);

        html[..Order(html, "section:customer")].Should().Contain("Acme RV Group");   // masthead
        html.Should().MatchRegex(@"@bottom-left\s*\{[^}]*Acme RV Group");            // running footer
        html[Order(html, "class=\"packet-foot\"")..].Should().Contain("Acme RV Group"); // static footer
        html.Should().NotContain("RV Intake");
    }

    [Fact]
    public void Render_WhenBrandingHasNoLogo_ShouldNotEmitAMastheadImage()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html[..Order(html, "section:customer")].Should().NotContain("<img");
    }

    [Fact]
    public void Render_WhenBrandingHasLogoDataUri_ShouldEmitItAsAMastheadImage()
    {
        var packet = FullPacket() with
        {
            Branding = new PacketBranding
            {
                BrandName = "RV Intake",
                LogoDataUri = "data:image/png;base64,iVBORw0KGgo=",
            },
        };

        var html = PacketHtmlRenderer.Render(packet);

        var masthead = html[..Order(html, "section:customer")];
        masthead.Should().Contain("class=\"masthead-logo\"");
        masthead.Should().Contain("src=\"data:image/png;base64,iVBORw0KGgo=\"");
    }

    [Fact]
    public void Render_ShouldPrecedeTheUnitHeadlineWithTheCustomerNameLastNameFirst()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("class=\"customer-headline\"");
        html.Should().Contain(">Gribble, Dale<");
        // Above the year/make/model <h1>, below the top refbox.
        html.IndexOf("Gribble, Dale", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("<h1>", StringComparison.Ordinal));
        html.IndexOf("Gribble, Dale", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("RVS #:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_WhenOnlyAFullNameIsAvailable_ShouldStillRenderACustomerHeadline()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());   // FullName only, no discrete parts

        html.Should().Contain("class=\"customer-headline\"");
        html.Should().Contain(">Jane Doe<");
    }

    // ── 3. Origin column renamed to "Location", Received row dropped (item 5) ──

    [Fact]
    public void Render_TheOriginColumn_ShouldBeHeadedLocation_WithNoReceivedRow()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        var originColumn = html[Order(html, "section:origin")..Order(html, "class=\"col unit\"")];
        originColumn.Should().Contain("<h2>Location</h2>");
        originColumn.Should().NotContain("Location &amp; received");
        originColumn.Should().NotContain("Received:", "the Received line now lives only in the top refbox");
    }

    [Fact]
    public void Render_ShouldRenderTheThreeColumnCustomerLocationUnitBand()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("class=\"idcols\"");
        html.Should().MatchRegex(@"\.idcols\b[^}]*display\s*:\s*grid");
        html.Should().Contain("class=\"col customer\"")
            .And.Contain("class=\"col origin\"")
            .And.Contain("class=\"col unit\"");
    }

    [Fact]
    public void Render_ShouldUseIdsFieldVocabulary()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("Complaint");          // not "In the customer's words"
        html.Should().Contain("Manufacturer:");      // not "Make"
        html.Should().Contain("Serial# (VIN):");     // not "VIN"
        html.Should().Contain("Preliminary assessment");
        html.Should().Contain("Reported symptoms &amp; diagnostic Q&amp;A");
    }

    [Fact]
    public void Render_ShouldEmitARunningFooterWithReferenceAndPageCounter()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        // @page margin box for engines that honour it (Chrome/Edge/Firefox print)…
        html.Should().Contain("@bottom-right")
            .And.Contain("counter(page)")
            .And.Contain("counter(pages)");
        html.Should().MatchRegex(@"@bottom-left\s*\{[^}]*RVS #A1B2C3D4");
        // …and a static end-of-flow footer for engines that don't (Safari).
        html.Should().Contain("class=\"packet-foot\"");
        html[Order(html, "section:status-link")..]
            .Should().Contain("RVS #A1B2C3D4");
    }

    [Fact]
    public void Render_RunningFooterContent_ShouldStayInsideACssString()
    {
        var packet = FullPacket() with
        {
            Origin = new PacketOrigin
            {
                LocationName = "Salt Lake Service Center",
                SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
                ReferenceCode = "A1B2C3D4",
            },
        };

        var html = PacketHtmlRenderer.Render(packet);

        // The reference code alphabet is ASCII with no quote/backslash, so the @page
        // content string is well-formed. Guard against a stray quote breaking the rule.
        var open = html.IndexOf("@bottom-left", StringComparison.Ordinal);
        var slice = html[open..html.IndexOf("@bottom-right", StringComparison.Ordinal)];
        slice.Split('"').Length.Should().Be(3, "content should be exactly one quoted string");
    }

    // ── 4. Category ────────────────────────────────────────────────────────

    [Fact]
    public void Render_WhenCategoryPresent_ShouldRenderIt()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html[Order(html, "section:category")..Order(html, "section:ai-summary")]
            .Should().Contain("Electrical");
    }

    [Fact]
    public void Render_WhenCategoryNull_ShouldRenderUncategorized()
    {
        var packet = FullPacket() with { IssueCategory = null };

        var html = PacketHtmlRenderer.Render(packet);

        html[Order(html, "section:category")..Order(html, "section:ai-summary")]
            .Should().Contain("Uncategorized");
    }

    // ── 5. Description, verbatim + encoded ─────────────────────────────────

    [Fact]
    public void Render_ShouldRenderTheCustomerDescriptionVerbatim_HtmlEncoded_PreservingWhitespace()
    {
        var packet = FullPacket() with
        {
            IssueDescription = "it won't \"start\" <b>at all</b> & smells hot\nsecond line",
        };

        var html = PacketHtmlRenderer.Render(packet);

        html.Should().NotContain("<b>at all</b>");
        html.Should().Contain("&lt;b&gt;at all&lt;/b&gt;");
        html.Should().Contain("&amp; smells hot");
        // newlines preserved for a shop reader — rendered inside a <pre>-style block
        html.Should().MatchRegex(@"smells hot\r?\nsecond line");
    }

    // ── 6. Diagnostic Q&A — the expert block ──────────────────────────────

    [Fact]
    public void Render_WhenDiagnosticsPresent_ShouldRenderEveryQuestionAndAnswer()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("Does the generator start at all?");
        html.Should().Contain("Yes, then dies");
        html.Should().Contain("Dies after about 10 minutes");
        html.Should().Contain("Any warning lights?");
        html.Should().Contain("Temp light");
    }

    [Fact]
    public void Render_TheDiagnosticsBlock_ShouldBeVisuallyEmphasised_NotByColourAlone()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("class=\"diagnostics\"");
        // emphasis is carried by a heavier border / weight in the embedded stylesheet,
        // legible in greyscale
        html.Should().MatchRegex(@"\.diagnostics\b[^}]*border[^}]*}");
    }

    [Fact]
    public void Render_WhenNoDiagnostics_ShouldRenderAnExplicitPlaceholder()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        // MinimalPacket has no sections after diagnostics, so slice to the end.
        html[Order(html, "section:diagnostics")..]
            .Should().Contain("No diagnostic questions were answered");
    }

    // ── 5. AI summary (rendered above the complaint) ──────────────────────

    [Fact]
    public void Render_WhenAiSummaryPresent_ShouldRenderItLabelledAsAiGenerated()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        var block = html[Order(html, "section:ai-summary")..Order(html, "section:description")];
        block.Should().Contain("Likely overheating on the generator windings.");
        block.ToLowerInvariant().Should().Contain("ai-generated");
    }

    [Fact]
    public void Render_WhenAiSummaryAbsent_ShouldOmitTheSection()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        html.Should().NotContain("section:ai-summary");
    }

    // ── 8. Photos ─────────────────────────────────────────────────────────

    [Fact]
    public void Render_WhenPhotosPresent_ShouldEmbedThemAsImgTagsWithUrls_NeverBase64()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("<img");
        html.Should().Contain("src=\"https://blob/generator.jpg?sas=read\"");
        html.Should().Contain("generator.jpg");
        html.ToLowerInvariant().Should().NotContain("data:image");
        html.ToLowerInvariant().Should().NotContain("base64");
    }

    [Fact]
    public void Render_ShouldHtmlEncodePhotoUrlsInTheSrcAttribute()
    {
        var packet = FullPacket() with
        {
            Photos = [new PacketPhoto { Url = "https://blob/x.jpg?a=1&b=2&sig=abc", FileName = "x.jpg" }],
        };

        var html = PacketHtmlRenderer.Render(packet);

        html.Should().Contain("a=1&amp;b=2&amp;sig=abc");
        html.Should().NotContain("a=1&b=2");
    }

    [Fact]
    public void Render_WhenNoPhotos_ShouldOmitTheSection()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        html.Should().NotContain("section:photos");
    }

    [Fact]
    public void Render_ShouldPageBreakPhotosBeyondTheSixthOntoAnAppendix()
    {
        var photos = Enumerable.Range(1, 8)
            .Select(i => new PacketPhoto { Url = $"https://blob/p{i}.jpg", FileName = $"p{i}.jpg" })
            .ToArray();
        var packet = FullPacket() with { Photos = photos };

        var html = PacketHtmlRenderer.Render(packet);

        // Spec B-2 item 8: up to 6 on page one, the rest on an appendix page.
        html.Should().MatchRegex(@"nth-child\(n\s*\+\s*7\)[^}]*break-before\s*:\s*page");
    }

    // ── 9. Paste block ───────────────────────────────────────────────────

    [Fact]
    public void Render_WhenPasteBlockPresent_ShouldRenderItInAMonospaceBlockVerbatim()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        var block = html[Order(html, "section:paste-block")..Order(html, "section:status-link")];
        block.Should().Contain("<pre");
        block.Should().Contain("ELECTRICAL");
        block.Should().MatchRegex(@"Generator quits after ten minutes\.\r?\nhttps://rvintake\.com/status/abc123");
    }

    [Fact]
    public void Render_WhenPasteBlockAbsent_ShouldOmitTheSection()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        html.Should().NotContain("section:paste-block");
    }

    // ── 10. Status link ─────────────────────────────────────────────────

    [Fact]
    public void Render_WhenStatusLinkPresent_ShouldRenderAnAnchorAndTheVisibleUrl()
    {
        var html = PacketHtmlRenderer.Render(FullPacket());

        html.Should().Contain("href=\"https://rvintake.com/status/abc123\"");
        html.Should().Contain(">https://rvintake.com/status/abc123<");
    }

    [Fact]
    public void Render_WhenStatusLinkAbsent_ShouldOmitTheSection()
    {
        var html = PacketHtmlRenderer.Render(MinimalPacket());

        html.Should().NotContain("section:status-link");
    }

    // ── Encoding / safety across every field ────────────────────────────

    [Fact]
    public void Render_ShouldHtmlEncodeEveryDynamicValue()
    {
        var packet = FullPacket() with
        {
            Customer = new PacketCustomer { FullName = "<script>alert('x')</script>" },
        };

        var html = PacketHtmlRenderer.Render(packet);

        html.Should().NotContain("<script>alert");
        html.Should().Contain("&lt;script&gt;alert");
    }

    [Fact]
    public void Render_WhenStatusLinkIsNotHttp_ShouldRenderItAsInertText_NotALink()
    {
        var packet = FullPacket() with { StatusLink = new PacketStatusLink { Url = "javascript:alert(1)" } };

        var html = PacketHtmlRenderer.Render(packet);

        html.Should().NotContain("href=\"javascript:");
    }

    [Fact]
    public void Render_ShouldBeDeterministic_ForTheSamePacket()
    {
        var packet = FullPacket();

        PacketHtmlRenderer.Render(packet).Should().Be(PacketHtmlRenderer.Render(packet));
    }
}
