using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="PhotoFindingsCleaner"/> — the gate between what the model says it read off
/// the customer's photos and what is stored on the request (issue #772): caps, trimming, and no
/// citation to a photo the request does not have.
/// </summary>
public class PhotoFindingsCleanerTests
{
    private static readonly string[] Known = ["att_1", "att_2", "att_3"];

    [Fact]
    public void Clean_WhenRawIsNull_ShouldReturnNull()
    {
        PhotoFindingsCleaner.Clean(null, Known).Should().BeNull();
    }

    [Fact]
    public void Clean_WhenKnownAttachmentIdsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => PhotoFindingsCleaner.Clean(new PhotoFindingsEmbedded(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Clean_WhenNothingSurvives_ShouldReturnNull()
    {
        var raw = new PhotoFindingsEmbedded
        {
            Observations = [new PhotoObservationEmbedded { Text = "  ", AttachmentId = "att_1" }],
        };

        PhotoFindingsCleaner.Clean(raw, Known).Should().BeNull();
    }

    [Fact]
    public void Clean_ShouldTrimEveryString_AndNullBlankOptionalFields()
    {
        var raw = new PhotoFindingsEmbedded
        {
            DataPlates =
            [
                new PhotoDataPlateEmbedded
                {
                    Component = " Refrigerator ", Manufacturer = " Dometic ", ModelNumber = " RM2652 ",
                    SerialNumber = "  ", AttachmentId = " att_1 ",
                },
            ],
            FaultCodes =
            [
                new PhotoFaultCodeEmbedded { Component = " Thermostat ", Code = " E1 ", Meaning = " ", AttachmentId = "att_2" },
            ],
            Observations = [new PhotoObservationEmbedded { Text = " Water staining on the ceiling panel ", AttachmentId = "att_3" }],
        };

        var result = PhotoFindingsCleaner.Clean(raw, Known)!;

        var plate = result.DataPlates.Should().ContainSingle().Subject;
        plate.Component.Should().Be("Refrigerator");
        plate.Manufacturer.Should().Be("Dometic");
        plate.ModelNumber.Should().Be("RM2652");
        plate.SerialNumber.Should().BeNull();
        plate.AttachmentId.Should().Be("att_1");

        var code = result.FaultCodes.Should().ContainSingle().Subject;
        code.Component.Should().Be("Thermostat");
        code.Code.Should().Be("E1");
        code.Meaning.Should().BeNull();

        result.Observations.Should().ContainSingle().Which.Text.Should().Be("Water staining on the ceiling panel");
    }

    [Fact]
    public void Clean_ShouldDropFindingsCitingAnUnknownAttachment()
    {
        var raw = new PhotoFindingsEmbedded
        {
            DataPlates = [new PhotoDataPlateEmbedded { Component = "Furnace", ModelNumber = "SF-35", AttachmentId = "att_99" }],
            FaultCodes = [new PhotoFaultCodeEmbedded { Component = "Generator", Code = "36", AttachmentId = "" }],
            Observations =
            [
                new PhotoObservationEmbedded { Text = "Torn awning fabric", AttachmentId = "att_2" },
                new PhotoObservationEmbedded { Text = "Cracked sealant", AttachmentId = "photo 3" },
            ],
        };

        var result = PhotoFindingsCleaner.Clean(raw, Known)!;

        result.DataPlates.Should().BeEmpty();
        result.FaultCodes.Should().BeEmpty();
        result.Observations.Should().ContainSingle().Which.Text.Should().Be("Torn awning fabric");
    }

    [Fact]
    public void Clean_ShouldDropADataPlate_WithNothingReadOffIt()
    {
        var raw = new PhotoFindingsEmbedded
        {
            DataPlates =
            [
                new PhotoDataPlateEmbedded { Component = "Water heater", AttachmentId = "att_1" },
                new PhotoDataPlateEmbedded { Component = " ", ModelNumber = "RM2652", AttachmentId = "att_1" },
            ],
        };

        PhotoFindingsCleaner.Clean(raw, Known).Should().BeNull();
    }

    [Fact]
    public void Clean_ShouldDropAFaultCode_WithoutAComponentOrCode()
    {
        var raw = new PhotoFindingsEmbedded
        {
            FaultCodes =
            [
                new PhotoFaultCodeEmbedded { Component = "Thermostat", Code = " ", AttachmentId = "att_1" },
                new PhotoFaultCodeEmbedded { Component = "", Code = "E1", AttachmentId = "att_1" },
            ],
        };

        PhotoFindingsCleaner.Clean(raw, Known).Should().BeNull();
    }

    [Fact]
    public void Clean_ShouldCapEachListAtFive_InOrder()
    {
        var raw = new PhotoFindingsEmbedded
        {
            DataPlates = [.. Enumerable.Range(1, 7).Select(i => new PhotoDataPlateEmbedded { Component = $"C{i}", ModelNumber = $"M{i}", AttachmentId = "att_1" })],
            FaultCodes = [.. Enumerable.Range(1, 7).Select(i => new PhotoFaultCodeEmbedded { Component = $"C{i}", Code = $"E{i}", AttachmentId = "att_1" })],
            Observations = [.. Enumerable.Range(1, 7).Select(i => new PhotoObservationEmbedded { Text = $"O{i}", AttachmentId = "att_1" })],
        };

        var result = PhotoFindingsCleaner.Clean(raw, Known)!;

        result.DataPlates.Select(p => p.Component).Should().Equal("C1", "C2", "C3", "C4", "C5");
        result.FaultCodes.Select(c => c.Code).Should().Equal("E1", "E2", "E3", "E4", "E5");
        result.Observations.Select(o => o.Text).Should().Equal("O1", "O2", "O3", "O4", "O5");
    }

    [Fact]
    public void Clean_ShouldCapAfterDroppingInvalidEntries()
    {
        var raw = new PhotoFindingsEmbedded
        {
            Observations =
            [
                new PhotoObservationEmbedded { Text = "bad", AttachmentId = "att_99" },
                .. Enumerable.Range(1, 5).Select(i => new PhotoObservationEmbedded { Text = $"O{i}", AttachmentId = "att_1" }),
            ],
        };

        PhotoFindingsCleaner.Clean(raw, Known)!.Observations.Should().HaveCount(5);
    }

    [Fact]
    public void Clean_ShouldTreatAnOverlongField_AsUnread()
    {
        var raw = new PhotoFindingsEmbedded
        {
            DataPlates =
            [
                new PhotoDataPlateEmbedded
                {
                    Component = "Refrigerator", ModelNumber = "RM2652",
                    SerialNumber = new string('9', PhotoFindingsCleaner.MaxFieldLength + 1), AttachmentId = "att_1",
                },
            ],
            Observations = [new PhotoObservationEmbedded { Text = new string('x', PhotoFindingsCleaner.MaxObservationLength + 1), AttachmentId = "att_1" }],
        };

        var result = PhotoFindingsCleaner.Clean(raw, Known)!;

        result.DataPlates.Single().SerialNumber.Should().BeNull();
        result.Observations.Should().BeEmpty();
    }
}
