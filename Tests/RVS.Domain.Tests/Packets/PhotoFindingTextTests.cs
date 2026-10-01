using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Packets;

namespace RVS.Domain.Tests.Packets;

/// <summary>
/// Tests for <see cref="PhotoFindingText"/> — the shared wording of a photo finding in the
/// packet's From photos sub-block and the DMS paste block (issue #772).
/// </summary>
public class PhotoFindingTextTests
{
    [Fact]
    public void PasteLines_WhenFindingsNull_ShouldBeEmpty()
    {
        PhotoFindingText.PasteLines(null).Should().BeEmpty();
    }

    [Fact]
    public void PasteLines_ShouldCarryDataPlatesThenFaultCodes_AndNoObservations()
    {
        var findings = new PhotoFindingsEmbedded
        {
            DataPlates = [new PhotoDataPlateEmbedded { Component = "Refrigerator", Manufacturer = "Dometic", ModelNumber = "RM2652", SerialNumber = "12345678", AttachmentId = "a" }],
            FaultCodes = [new PhotoFaultCodeEmbedded { Component = "Thermostat", Code = "E1", AttachmentId = "a" }],
            Observations = [new PhotoObservationEmbedded { Text = "Water staining", AttachmentId = "a" }],
        };

        PhotoFindingText.PasteLines(findings).Should().Equal(
            "EQUIPMENT: Refrigerator — Dometic RM2652 · S/N 12345678",
            "FAULT CODE: Thermostat — code E1");
    }

    [Fact]
    public void DataPlate_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => PhotoFindingText.DataPlate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FaultCode_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => PhotoFindingText.FaultCode(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
