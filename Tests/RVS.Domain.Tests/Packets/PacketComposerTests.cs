using System.Collections;
using System.Reflection;
using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PacketComposer"/> — the pure transform that assembles a
/// <see cref="ServicePacket"/> from a <see cref="ServiceRequest"/> plus a
/// <see cref="PacketCompositionContext"/>, per <c>Spec B-2</c>.
/// </summary>
public class PacketComposerTests
{
    // ── Fixtures ────────────────────────────────────────────────────────────

    private static ServiceRequest FullyPopulatedRequest(
        string id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890") => new()
    {
        Id = id,
        TenantId = "ten_1",
        LocationId = "loc_1",
        Status = "New",
        IssueCategory = "Electrical",
        IssueDescription = "  Generator quits after ten minutes. Smells hot.  ",
        TechnicianSummary = "  Likely overheating on the generator windings.  ",
        Priority = "High",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Dale",
            LastName = "Gribble",
            Email = "dale@example.com",
            Phone = "555-0101",
        },
        AssetInfo = new AssetInfoEmbedded
        {
            AssetId = "1FDXE45S12HB00001",
            Manufacturer = "Winnebago",
            Model = "View",
            Year = 2021,
        },
        DiagnosticResponses =
        [
            new DiagnosticResponseEmbedded
            {
                QuestionText = "Does the generator start at all?",
                SelectedOptions = ["Yes, then dies"],
                FreeTextResponse = "Dies after about 10 minutes",
            },
            new DiagnosticResponseEmbedded
            {
                QuestionText = "Any warning lights?",
                SelectedOptions = ["Temp light"],
                FreeTextResponse = null,
            },
        ],
        Attachments =
        [
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_photo_1",
                FileName = "generator.jpg",
                ContentType = "image/jpeg",
                BlobUri = "https://blob/generator.jpg",
            },
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_voice_1",
                FileName = "note.m4a",
                ContentType = "audio/mp4",
                BlobUri = "https://blob/note.m4a",
            },
        ],
    };

    private static PacketCompositionContext FullContext() => new()
    {
        LocationName = "Salt Lake Service Center",
        LocationPhone = "555-0199",
        SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
        StatusLinkUrl = "https://rvintake.com/status/abc123",
        PasteBlock = "ELECTRICAL\nGenerator quits after ten minutes.\nhttps://rvintake.com/status/abc123",
        PhotoUrls = new Dictionary<string, string>
        {
            ["att_photo_1"] = "https://blob/generator.jpg?sas=read",
        },
    };

    // ── Guard clauses ───────────────────────────────────────────────────────

    [Fact]
    public void Compose_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketComposer.Compose(null!, FullContext());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Compose_WhenContextIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PacketComposer.Compose(FullyPopulatedRequest(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public void Compose_WhenFullyPopulated_ShouldMapEverySectionInB2Order()
    {
        var packet = PacketComposer.Compose(FullyPopulatedRequest(), FullContext());

        // 1. Unit header
        packet.Unit.Year.Should().Be(2021);
        packet.Unit.Make.Should().Be("Winnebago");
        packet.Unit.Model.Should().Be("View");
        packet.Unit.Vin.Should().Be("1FDXE45S12HB00001");
        packet.Unit.HasVin.Should().BeTrue();

        // 2. Customer + preferred contact
        packet.Customer.FullName.Should().Be("Dale Gribble");
        packet.Customer.Phone.Should().Be("555-0101");
        packet.Customer.Email.Should().Be("dale@example.com");

        // 3. Location + timestamp + reference code
        packet.Origin.LocationName.Should().Be("Salt Lake Service Center");
        packet.Origin.LocationPhone.Should().Be("555-0199");
        packet.Origin.SubmittedAtUtc.Should().Be(new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero));
        packet.Origin.ReferenceCode.Should().Be("A1B2C3D4");

        // 4. Issue category
        packet.IssueCategory.Should().Be("Electrical");

        // 5. Description, verbatim
        packet.IssueDescription.Should().Be("  Generator quits after ten minutes. Smells hot.  ");

        // 6. Diagnostic Q&A
        packet.Diagnostics.Should().HaveCount(2);
        packet.Diagnostics[0].Question.Should().Be("Does the generator start at all?");
        packet.Diagnostics[0].Answers.Should().Equal("Yes, then dies", "Dies after about 10 minutes");

        // 7. AI summary, labelled AI-generated
        packet.AiSummary.Should().NotBeNull();
        packet.AiSummary!.Text.Should().Be("Likely overheating on the generator windings.");
        packet.AiSummary.IsAiGenerated.Should().BeTrue();

        // 8. Photos
        packet.Photos.Should().ContainSingle();
        packet.Photos[0].Url.Should().Be("https://blob/generator.jpg?sas=read");
        packet.Photos[0].FileName.Should().Be("generator.jpg");

        // 9. Paste block
        packet.PasteBlock.Should().Be("ELECTRICAL\nGenerator quits after ten minutes.\nhttps://rvintake.com/status/abc123");

        // 10. Status link
        packet.StatusLink.Should().NotBeNull();
        packet.StatusLink!.Url.Should().Be("https://rvintake.com/status/abc123");
    }

    [Fact]
    public void Compose_WhenIdHasNoHyphen_ShouldUpperCaseWholeIdAsReferenceCode()
    {
        var request = FullyPopulatedRequest("abc123");

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Origin.ReferenceCode.Should().Be("ABC123");
    }

    // ── Degradation: VIN ────────────────────────────────────────────────────

    [Fact]
    public void Compose_WhenVinAbsent_ShouldDegradeUnitHeader()
    {
        var request = FullyPopulatedRequest();
        request.AssetInfo = new AssetInfoEmbedded
        {
            AssetId = "   ",
            Manufacturer = "Winnebago",
            Model = "View",
            Year = 2021,
        };

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Unit.HasVin.Should().BeFalse();
        packet.Unit.Vin.Should().BeNull();
        packet.Unit.Year.Should().Be(2021);
        packet.Unit.Make.Should().Be("Winnebago");
        packet.Unit.Model.Should().Be("View");
    }

    [Fact]
    public void Compose_WhenYearMakeModelAllAbsent_ShouldLeaveUnitFieldsNull()
    {
        var request = FullyPopulatedRequest();
        request.AssetInfo = new AssetInfoEmbedded { AssetId = "1FDXE45S12HB00001" };

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Unit.Year.Should().BeNull();
        packet.Unit.Make.Should().BeNull();
        packet.Unit.Model.Should().BeNull();
        packet.Unit.Vin.Should().Be("1FDXE45S12HB00001");
    }

    // ── Degradation: category ───────────────────────────────────────────────

    [Fact]
    public void Compose_WhenIssueCategoryBlank_ShouldSetCategoryNull()
    {
        var request = FullyPopulatedRequest();
        request.IssueCategory = "   ";

        var packet = PacketComposer.Compose(request, FullContext());

        packet.IssueCategory.Should().BeNull();
    }

    // ── Degradation: diagnostics ────────────────────────────────────────────

    [Fact]
    public void Compose_WhenNoDiagnosticResponses_ShouldReturnEmptyDiagnostics()
    {
        var request = FullyPopulatedRequest();
        request.DiagnosticResponses = [];

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Diagnostics.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Compose_WhenDiagnosticQuestionBlank_ShouldSkipThatEntry()
    {
        var request = FullyPopulatedRequest();
        request.DiagnosticResponses =
        [
            new DiagnosticResponseEmbedded { QuestionText = "  ", SelectedOptions = ["orphan"] },
            new DiagnosticResponseEmbedded { QuestionText = "Real question?", SelectedOptions = ["Yes"] },
        ];

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Diagnostics.Should().ContainSingle();
        packet.Diagnostics[0].Question.Should().Be("Real question?");
    }

    [Fact]
    public void Compose_ShouldCombineSelectedOptionsAndFreeTextIntoAnswers()
    {
        var request = FullyPopulatedRequest();
        request.DiagnosticResponses =
        [
            new DiagnosticResponseEmbedded
            {
                QuestionText = "What have you tried?",
                SelectedOptions = ["Reset breaker", "Checked fuel"],
                FreeTextResponse = "Also swapped the battery",
            },
        ];

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Diagnostics[0].Answers.Should().Equal("Reset breaker", "Checked fuel", "Also swapped the battery");
    }

    [Fact]
    public void Compose_WhenDiagnosticFreeTextBlank_ShouldOmitItFromAnswers()
    {
        var request = FullyPopulatedRequest();
        request.DiagnosticResponses =
        [
            new DiagnosticResponseEmbedded
            {
                QuestionText = "Warning lights?",
                SelectedOptions = ["Temp light"],
                FreeTextResponse = "   ",
            },
        ];

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Diagnostics[0].Answers.Should().Equal("Temp light");
    }

    // ── Degradation: photos ─────────────────────────────────────────────────

    [Fact]
    public void Compose_WhenNoImageAttachments_ShouldReturnEmptyPhotos()
    {
        var request = FullyPopulatedRequest();
        request.Attachments =
        [
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_voice_1",
                FileName = "note.m4a",
                ContentType = "audio/mp4",
            },
        ];

        var packet = PacketComposer.Compose(request, FullContext());

        packet.Photos.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Compose_WhenImageAttachmentHasNoUrlInContext_ShouldOmitThatPhoto()
    {
        var request = FullyPopulatedRequest();
        request.Attachments =
        [
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_photo_1",
                FileName = "generator.jpg",
                ContentType = "image/jpeg",
            },
            new ServiceRequestAttachmentEmbedded
            {
                AttachmentId = "att_photo_2",
                FileName = "panel.jpg",
                ContentType = "image/jpeg",
            },
        ];

        // context only resolves att_photo_1
        var packet = PacketComposer.Compose(request, FullContext());

        packet.Photos.Should().ContainSingle();
        packet.Photos[0].FileName.Should().Be("generator.jpg");
    }

    // ── Degradation: AI summary ─────────────────────────────────────────────

    [Fact]
    public void Compose_WhenTechnicianSummaryBlank_ShouldSetAiSummaryNull()
    {
        var request = FullyPopulatedRequest();
        request.TechnicianSummary = "   ";

        var packet = PacketComposer.Compose(request, FullContext());

        packet.AiSummary.Should().BeNull();
    }

    [Fact]
    public void Compose_WhenTechnicianSummaryNull_ShouldSetAiSummaryNull()
    {
        var request = FullyPopulatedRequest();
        request.TechnicianSummary = null;

        var packet = PacketComposer.Compose(request, FullContext());

        packet.AiSummary.Should().BeNull();
    }

    // ── Verbatim description ────────────────────────────────────────────────

    [Fact]
    public void Compose_ShouldCopyIssueDescriptionVerbatim()
    {
        var request = FullyPopulatedRequest();
        request.IssueDescription = "\tit won't \"start\" — tried everything…  \n";

        var packet = PacketComposer.Compose(request, FullContext());

        packet.IssueDescription.Should().Be("\tit won't \"start\" — tried everything…  \n");
    }

    // ── Degradation: context-supplied fields ───────────────────────────────

    [Fact]
    public void Compose_WhenPasteBlockAndStatusLinkAbsentFromContext_ShouldLeaveThemNull()
    {
        var context = new PacketCompositionContext
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
        };

        var packet = PacketComposer.Compose(FullyPopulatedRequest(), context);

        packet.PasteBlock.Should().BeNull();
        packet.StatusLink.Should().BeNull();
        packet.Origin.LocationName.Should().BeNull();
        packet.Origin.LocationPhone.Should().BeNull();
        packet.Photos.Should().BeEmpty();
    }

    [Fact]
    public void Compose_WhenStatusLinkUrlBlank_ShouldSetStatusLinkNull()
    {
        var context = FullContext() with { StatusLinkUrl = "   " };

        var packet = PacketComposer.Compose(FullyPopulatedRequest(), context);

        packet.StatusLink.Should().BeNull();
    }

    // ── Never leaks pricing / other-customer data ──────────────────────────

    [Fact]
    public void Compose_ShouldNeverExposePricingOrServiceEventData()
    {
        var request = FullyPopulatedRequest();
        request.Priority = "High";
        request.ServiceEvent = new ServiceEventEmbedded
        {
            LaborHours = 4.5m,
            RepairAction = "Replaced generator",
            PartsUsed = ["gen-assembly"],
        };

        var packet = PacketComposer.Compose(request, FullContext());

        var forbidden = new[]
        {
            "price", "pricing", "cost", "labor", "labour", "quote",
            "partsused", "laborhours", "serviceevent", "repairaction",
        };
        var names = CollectPropertyNames(typeof(ServicePacket), []);

        names.Should().NotContain(n => forbidden.Any(f => n.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    private static HashSet<string> CollectPropertyNames(Type type, HashSet<Type> seen)
    {
        var names = new HashSet<string>();
        if (!seen.Add(type))
        {
            return names;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            names.Add(prop.Name);

            var t = prop.PropertyType;
            if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
            {
                t = t.GetGenericArguments()[^1];
            }

            if (t.Namespace == "RVS.Domain.Packets")
            {
                names.UnionWith(CollectPropertyNames(t, seen));
            }
        }

        return names;
    }
}
