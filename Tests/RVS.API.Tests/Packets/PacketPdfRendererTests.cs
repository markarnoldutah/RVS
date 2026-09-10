using System.Diagnostics;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.API.Packets;
using RVS.Domain.Packets;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace RVS.API.Tests.Packets;

/// <summary>
/// Tests for the PDF packet renderer (<c>Spec B-3</c>, <c>B-7</c>, issue <c>#432</c>).
///
/// The renderer is split so its content can be tested as pure logic:
/// <list type="bullet">
///   <item><see cref="PacketPdfLayout.Build"/> resolves a <see cref="ServicePacket"/> into
///   an ordered, degradation-resolved layout model — the single place that decides
///   <em>what</em> content appears in <em>what</em> order. This is asserted exhaustively
///   and cross-checked against <see cref="PacketHtmlRenderer"/> so the two renderings
///   cannot diverge (issue <c>#432</c> AC "output matches the HTML rendering").</item>
///   <item><see cref="PacketPdfRenderer.Render(ServicePacket, System.Collections.Generic.IReadOnlyDictionary{string, byte[]})"/>
///   paints that model with QuestPDF. Only byte-level shape, determinism and latency are
///   asserted here — QuestPDF's own layout engine is not re-tested.</item>
/// </list>
/// </summary>
public class PacketPdfRendererTests
{
    // ── Fixtures (mirrors PacketHtmlRendererTests) ──────────────────────────

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

    // 1x1 transparent PNG — a valid image payload for the embed / latency tests.
    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static PacketPdfLayout Layout(ServicePacket packet) => PacketPdfLayout.Build(packet);

    private static PacketPdfLayoutSection Section(ServicePacket packet, string id) =>
        Layout(packet).Sections.Single(s => s.Id == id);

    private static int Order(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "marker '{0}' should be present", marker);
        return index;
    }

    // Spec B-2 order, with the AI assessment lifted above the complaint.
    private static readonly string[] SectionIdsInB2Order =
    [
        "unit", "customer", "origin", "category", "ai-summary",
        "description", "diagnostics", "photos", "paste-block", "status-link",
    ];

    // ── Guard clause ───────────────────────────────────────────────────────

    [Fact]
    public void Render_WhenPacketIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketPdfRenderer.Render(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_WhenPacketIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketPdfLayout.Build(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── PDF document shape ─────────────────────────────────────────────────

    [Fact]
    public void Render_ShouldProduceANonEmptyPdfDocument()
    {
        var bytes = PacketPdfRenderer.Render(FullPacket());

        bytes.Should().NotBeNullOrEmpty();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-", "the output must be a PDF");
    }

    [Fact]
    public void Render_WithMinimalPacket_ShouldNotThrow()
    {
        var act = () => PacketPdfRenderer.Render(MinimalPacket());

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_ShouldBeDeterministic_ForTheSamePacket()
    {
        var first = PacketPdfRenderer.Render(FullPacket());
        var second = PacketPdfRenderer.Render(FullPacket());

        second.Should().Equal(first, "document metadata dates are pinned to the packet so output is reproducible");
    }

    [Fact]
    public void Render_ShouldEmbedProvidedPhotoBytes_WithoutThrowing()
    {
        var png = OnePixelPng();
        var packet = FullPacket();
        var images = packet.Photos.ToDictionary(p => p.Url, _ => png);

        var act = () => PacketPdfRenderer.Render(packet, images);

        act.Should().NotThrow();
    }

    // Minimal ISO-BMFF header whose `ftyp` major brand is "heic" — the shape of an iPhone
    // photo. QuestPDF's decoder cannot read it (issue #492 item 8).
    private static byte[] HeicHeader() =>
    [
        0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, // ....ftyp
        0x68, 0x65, 0x69, 0x63, 0x00, 0x00, 0x00, 0x00, // heic....
        0x68, 0x65, 0x69, 0x63, 0x6D, 0x69, 0x66, 0x31, // heicmif1
    ];

    [Fact]
    public void Render_WhenAPhotosBytesAreHeic_ShouldRenderAPlaceholder_NotThrowOrDropTheGrid()
    {
        var packet = FullPacket();
        var images = packet.Photos.ToDictionary(p => p.Url, _ => HeicHeader());

        var act = () => PacketPdfRenderer.Render(packet, images);

        act.Should().NotThrow("an undecodable image must fall back to a placeholder, never fail the whole PDF");
        PacketPdfRenderer.Render(packet, images).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Render_WhenPhotoBytesAreAHeicTranscodedToJpeg_ShouldEmbedTheImage_NotAPlaceholder()
    {
        // #508: once AttachmentService transcodes the HEIC upload, the packet pipeline hands
        // the renderer a JPEG the SkiaSharp decoder reads — so the photo embeds instead of
        // falling back to the labelled placeholder that a raw HEIC still triggers.
        var transcoder = new MagickImageTranscoder(
            MsOptions.Create(new ImageTranscodeOptions()),
            Mock.Of<ILogger<MagickImageTranscoder>>());
        var jpeg = transcoder.TranscodeToJpeg(SampleImages.Heic96x64());
        jpeg.Should().NotBeNull("the HEIC fixture must transcode for this regression to be meaningful");

        var packet = FullPacket();
        var withJpeg = packet.Photos.ToDictionary(p => p.Url, _ => jpeg!.JpegBytes);
        var withHeic = packet.Photos.ToDictionary(p => p.Url, _ => SampleImages.Heic96x64());

        var jpegPdf = PacketPdfRenderer.Render(packet, withJpeg);
        var heicPdf = PacketPdfRenderer.Render(packet, withHeic);

        jpegPdf.Should().NotBeNullOrEmpty();
        // The embedded photo makes the JPEG rendering materially larger than the
        // placeholder-only rendering of the same packet.
        jpegPdf.Length.Should().BeGreaterThan(heicPdf.Length);
    }

    [Fact]
    public void Render_WhenAPhotosBytesAreEmpty_ShouldRenderAPlaceholder_NotThrow()
    {
        var packet = FullPacket();
        var images = packet.Photos.ToDictionary(p => p.Url, _ => Array.Empty<byte>());

        var act = () => PacketPdfRenderer.Render(packet, images);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithABrandNameOverride_ShouldNotThrow()
    {
        var packet = FullPacket() with { Branding = new PacketBranding { BrandName = "Acme RV Group" } };

        var act = () => PacketPdfRenderer.Render(packet);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithALogoDataUri_ShouldDecodeAndEmbedItWithoutThrowing()
    {
        var pngDataUri = "data:image/png;base64," + Convert.ToBase64String(OnePixelPng());
        var packet = FullPacket() with
        {
            Branding = new PacketBranding { BrandName = "RV Intake", LogoDataUri = pngDataUri },
        };

        var act = () => PacketPdfRenderer.Render(packet);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithAnUndecodableLogoDataUri_ShouldFallBackToNoLogo_NotThrow()
    {
        var packet = FullPacket() with
        {
            Branding = new PacketBranding { BrandName = "RV Intake", LogoDataUri = "data:image/png;base64,not-base64!!" },
        };

        var act = () => PacketPdfRenderer.Render(packet);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithSixPhotos_ShouldCompleteWellWithinTheLatencyBudget()
    {
        var png = OnePixelPng();
        var photos = Enumerable.Range(1, 6)
            .Select(i => new PacketPhoto { Url = $"https://blob/p{i}.jpg", FileName = $"p{i}.jpg" })
            .ToArray();
        var images = photos.ToDictionary(p => p.Url, _ => png);
        var packet = FullPacket() with { Photos = photos };

        var stopwatch = Stopwatch.StartNew();
        var bytes = PacketPdfRenderer.Render(packet, images);
        stopwatch.Stop();

        bytes.Should().NotBeNullOrEmpty();
        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(5),
            "Spec B-1 budgets the whole generation pipeline at P95 < 10 s; the PDF render alone must be a small fraction");
    }

    // ── Section ordering (Spec B-2) ────────────────────────────────────────

    [Fact]
    public void Build_ShouldEmitEverySectionInSpecB2Order()
    {
        var text = Layout(FullPacket()).ToPlainText();

        SectionIdsInB2Order
            .Select(id => Order(text, $"[section:{id}]"))
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public void Build_SectionOrder_ShouldMatchTheHtmlRenderer()
    {
        var packet = FullPacket();
        var html = PacketHtmlRenderer.Render(packet);
        var pdf = Layout(packet).ToPlainText();

        var htmlOrder = SectionIdsInB2Order.Select(id => Order(html, $"section:{id}")).ToList();
        var pdfOrder = SectionIdsInB2Order.Select(id => Order(pdf, $"[section:{id}]")).ToList();

        htmlOrder.Should().BeInAscendingOrder();
        pdfOrder.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Build_WhenOptionalSectionsAbsent_ShouldOmitTheSameOnesAsTheHtmlRenderer()
    {
        var packet = MinimalPacket();
        var pdf = Layout(packet).ToPlainText();
        var html = PacketHtmlRenderer.Render(packet);

        foreach (var id in new[] { "ai-summary", "photos", "paste-block", "status-link" })
        {
            pdf.Should().NotContain($"[section:{id}]");
            html.Should().NotContain($"section:{id}");
        }

        foreach (var id in new[] { "unit", "customer", "origin", "category", "description", "diagnostics" })
        {
            pdf.Should().Contain($"[section:{id}]");
        }
    }

    // ── 1. Unit header ────────────────────────────────────────────────────

    [Fact]
    public void Build_WhenVinPresent_ShouldIncludeASerialRow()
    {
        var section = Section(FullPacket(), "unit");

        section.Heading.Should().Be("2021 Winnebago View");
        section.Rows.Should().ContainSingle(r => r.Label == "Serial# (VIN)" && r.Value == "1FDXE45S12HB00001");
        section.Rows.Should().Contain(r => r.Label == "Manufacturer" && r.Value == "Winnebago");
    }

    [Fact]
    public void Build_WhenVinAbsent_ShouldOmitTheSerialRow()
    {
        var packet = FullPacket() with
        {
            Unit = new PacketUnitHeader { Year = 2021, Make = "Winnebago", Model = "View" },
        };

        Section(packet, "unit").Rows.Should().NotContain(r => r.Label == "Serial# (VIN)");
    }

    [Fact]
    public void Build_WhenYearMakeModelAllAbsent_ShouldUseAFallbackHeading()
    {
        Section(MinimalPacket(), "unit").Heading.Should().Be("Unit details not provided");
    }

    // ── 2. Customer ──────────────────────────────────────────────────────

    [Fact]
    public void Build_ShouldCarryCustomerName_AndOnlyTheContactRowsThatArePresent()
    {
        var section = Section(MinimalPacket(), "customer");

        section.Heading.Should().Be("Customer");
        section.Body.Should().Be("Jane Doe");
        section.Rows.Should().BeEmpty("the minimal packet has no phone, email or preferred contact");
    }

    [Fact]
    public void Build_ShouldIncludePreferredContactRow_WhenPresent()
    {
        Section(FullPacket(), "customer").Rows
            .Should().ContainSingle(r => r.Label == "Preferred contact" && r.Value == "Phone");
    }

    // ── 3. Origin ────────────────────────────────────────────────────────

    [Fact]
    public void Build_TheOriginSection_ShouldBeHeadedLocation_CarryLocationAndRvsNumber_AndNoReceivedRow()
    {
        var section = Section(FullPacket(), "origin");

        section.Heading.Should().Be("Location", "the Received line moved to the top of the masthead (issue #492 item 5)");
        section.Rows.Should().Contain(r => r.Label == "Location" && r.Value == "Salt Lake Service Center");
        section.Rows.Should().Contain(r => r.Label == "RVS #" && r.Value == "A1B2C3D4");
        section.Rows.Should().NotContain(r => r.Label == "Received");
    }

    [Fact]
    public void Build_ShouldExposeAnInvariantReceivedDisplayForTheTopOfTheMasthead()
    {
        Layout(FullPacket()).ReceivedDisplay.Should().Be("2026-09-05 14:30 UTC");
    }

    [Fact]
    public void Build_ShouldNormaliseTheSubmittedTimestampToUtc()
    {
        var packet = FullPacket() with
        {
            Origin = FullPacket().Origin with
            {
                SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 9, 30, 0, TimeSpan.FromHours(-5)),
            },
        };

        Layout(packet).ReceivedDisplay.Should().Be("2026-09-05 14:30 UTC");
    }

    [Fact]
    public void Build_ShouldExposeTheCustomerHeadlineFamilyNameFirst()
    {
        Layout(FullPacket()).CustomerHeadline.Should().Be("Gribble, Dale");
    }

    [Fact]
    public void ToPlainText_ShouldLeadWithTheReceivedLineAndCustomerHeadline_BeforeTheFirstSection()
    {
        var text = Layout(FullPacket()).ToPlainText();

        Order(text, "Received: 2026-09-05 14:30 UTC").Should().BeLessThan(Order(text, "Gribble, Dale"));
        Order(text, "Gribble, Dale").Should().BeLessThan(Order(text, "[section:unit]"));
    }

    // ── 4. Category ──────────────────────────────────────────────────────

    [Fact]
    public void Build_WhenCategoryPresent_ShouldUseIt()
    {
        Section(FullPacket(), "category").Body.Should().Be("Electrical");
    }

    [Fact]
    public void Build_WhenCategoryNull_ShouldFallBackToUncategorized()
    {
        Section(FullPacket() with { IssueCategory = null }, "category").Body.Should().Be("Uncategorized");
    }

    // ── 5. Description, verbatim ─────────────────────────────────────────

    [Fact]
    public void Build_ShouldCarryTheCustomerDescriptionVerbatim_NotEncoded_PreservingNewlines()
    {
        var packet = FullPacket() with
        {
            IssueDescription = "it won't \"start\" <b>at all</b> & smells hot\nsecond line",
        };

        // The PDF renders text directly — there is no markup to escape, so the value is
        // carried through byte-for-byte, unlike the HTML renderer which entity-encodes it.
        Section(packet, "description").Verbatim
            .Should().Be("it won't \"start\" <b>at all</b> & smells hot\nsecond line");
    }

    // ── 6. Diagnostic Q&A — the expert block ────────────────────────────

    [Fact]
    public void Build_WhenDiagnosticsPresent_ShouldCarryEveryQuestionAndAnswer_AndBeEmphasised()
    {
        var section = Section(FullPacket(), "diagnostics");

        section.Emphasised.Should().BeTrue("the diagnostic block is the visually dominant one");
        section.Diagnostics.Should().HaveCount(2);
        section.Diagnostics[0].Question.Should().Be("Does the generator start at all?");
        section.Diagnostics[0].Answers.Should().Equal("Yes, then dies", "Dies after about 10 minutes");
        section.Diagnostics[1].Question.Should().Be("Any warning lights?");
        section.Diagnostics[1].Answers.Should().Equal("Temp light");
    }

    [Fact]
    public void Build_WhenNoDiagnostics_ShouldCarryAnExplicitPlaceholder()
    {
        var section = Section(MinimalPacket(), "diagnostics");

        section.DiagnosticsEmpty.Should().BeTrue();
        section.Diagnostics.Should().BeEmpty();
        Layout(MinimalPacket()).ToPlainText().Should().Contain("No diagnostic questions were answered");
    }

    // ── 7. AI summary ───────────────────────────────────────────────────

    [Fact]
    public void Build_WhenAiSummaryPresent_ShouldCarryItFlaggedAsAiGenerated()
    {
        var section = Section(FullPacket(), "ai-summary");

        section.AiGeneratedTag.Should().BeTrue();
        section.Body.Should().Be("Likely overheating on the generator windings.");
        Layout(FullPacket()).ToPlainText().ToLowerInvariant().Should().Contain("ai-generated");
    }

    [Fact]
    public void Build_WhenAiSummaryAbsent_ShouldOmitTheSection()
    {
        Layout(MinimalPacket()).Sections.Should().NotContain(s => s.Id == "ai-summary");
    }

    // ── 8. Photos ───────────────────────────────────────────────────────

    [Fact]
    public void Build_WhenPhotosPresent_ShouldListThem_UpToSixOnTheFirstPage()
    {
        var section = Section(FullPacket(), "photos");

        section.Photos.Should().ContainSingle(p => p.FileName == "generator.jpg");
        section.PhotosOnFirstPage.Should().Be(1);
        section.HasAppendix.Should().BeFalse();
    }

    [Fact]
    public void Build_WhenMoreThanSixPhotos_ShouldPageTheRestToAnAppendix()
    {
        var photos = Enumerable.Range(1, 8)
            .Select(i => new PacketPhoto { Url = $"https://blob/p{i}.jpg", FileName = $"p{i}.jpg" })
            .ToArray();

        var section = Section(FullPacket() with { Photos = photos }, "photos");

        section.Photos.Should().HaveCount(8);
        section.PhotosOnFirstPage.Should().Be(6);
        section.HasAppendix.Should().BeTrue();
    }

    [Fact]
    public void Build_WithExactlySixPhotos_ShouldNotPageToAnAppendix()
    {
        var photos = Enumerable.Range(1, 6)
            .Select(i => new PacketPhoto { Url = $"https://blob/p{i}.jpg", FileName = $"p{i}.jpg" })
            .ToArray();

        var section = Section(FullPacket() with { Photos = photos }, "photos");

        section.PhotosOnFirstPage.Should().Be(6);
        section.HasAppendix.Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldIgnorePhotosWithoutAnHttpUrl()
    {
        var photos = new[]
        {
            new PacketPhoto { Url = "https://blob/ok.jpg", FileName = "ok.jpg" },
            new PacketPhoto { Url = "", FileName = "empty.jpg" },
            new PacketPhoto { Url = "data:image/png;base64,AAAA", FileName = "inline.png" },
        };

        Section(FullPacket() with { Photos = photos }, "photos").Photos
            .Should().ContainSingle().Which.FileName.Should().Be("ok.jpg");
    }

    [Fact]
    public void Build_WhenNoRenderablePhotos_ShouldOmitTheSection()
    {
        var packet = FullPacket() with
        {
            Photos = [new PacketPhoto { Url = "data:image/png;base64,AAAA", FileName = "inline.png" }],
        };

        Layout(packet).Sections.Should().NotContain(s => s.Id == "photos");
    }

    // ── 9. Paste block ─────────────────────────────────────────────────

    [Fact]
    public void Build_WhenPasteBlockPresent_ShouldCarryItVerbatim()
    {
        Section(FullPacket(), "paste-block").Verbatim
            .Should().Be("ELECTRICAL\nGenerator quits after ten minutes.\nhttps://rvintake.com/status/abc123");
    }

    [Fact]
    public void Build_WhenPasteBlockAbsent_ShouldOmitTheSection()
    {
        Layout(MinimalPacket()).Sections.Should().NotContain(s => s.Id == "paste-block");
    }

    // ── 10. Status link ───────────────────────────────────────────────

    [Fact]
    public void Build_WhenStatusLinkIsHttp_ShouldCarryItAsAnActiveLink()
    {
        var section = Section(FullPacket(), "status-link");

        section.Link.Should().NotBeNull();
        section.Link!.Url.Should().Be("https://rvintake.com/status/abc123");
        section.Link.Active.Should().BeTrue();
    }

    [Fact]
    public void Build_WhenStatusLinkIsNotHttp_ShouldCarryItAsInertText()
    {
        var packet = FullPacket() with { StatusLink = new PacketStatusLink { Url = "javascript:alert(1)" } };

        Section(packet, "status-link").Link!.Active.Should().BeFalse();
    }

    [Fact]
    public void Build_WhenStatusLinkAbsent_ShouldOmitTheSection()
    {
        Layout(MinimalPacket()).Sections.Should().NotContain(s => s.Id == "status-link");
    }

    // ── Parity with the HTML rendering (issue #432 AC) ─────────────────

    [Fact]
    public void Build_ForAFullPacket_ShouldCarryTheSameKeyValuesAsTheHtmlRendering_InTheSameOrder()
    {
        var packet = FullPacket();
        var html = PacketHtmlRenderer.Render(packet);
        var pdf = Layout(packet).ToPlainText();

        string[] values =
        [
            "2026-09-05 14:30 UTC",
            "Gribble, Dale",
            "2021 Winnebago View",
            "1FDXE45S12HB00001",
            "Dale Gribble",
            "555-0101",
            "Salt Lake Service Center",
            "A1B2C3D4",
            "Electrical",
            "Likely overheating on the generator windings.",
            "Generator quits after ten minutes. Smells hot.",
            "Does the generator start at all?",
            "Dies after about 10 minutes",
            "generator.jpg",
            "https://rvintake.com/status/abc123",
        ];

        foreach (var value in values)
        {
            html.Should().Contain(WebUtility.HtmlEncode(value));
            pdf.Should().Contain(value);
        }

        values.Select(v => pdf.IndexOf(v, StringComparison.Ordinal))
            .Should().BeInAscendingOrder("the PDF presents the same content in the same section order as the HTML");
    }

    [Fact]
    public void Build_ShouldBeDeterministic_ForTheSamePacket()
    {
        Layout(FullPacket()).ToPlainText().Should().Be(Layout(FullPacket()).ToPlainText());
    }
}
