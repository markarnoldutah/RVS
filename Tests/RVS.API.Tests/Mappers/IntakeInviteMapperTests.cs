using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Mappers;

/// <summary>
/// Tests for <see cref="IntakeInviteMapper"/> (<c>Spec A-14</c>, issue #663).
/// </summary>
public class IntakeInviteMapperTests
{
    private static readonly DateTime Created = new(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc);

    private static IntakeInvite BuildInvite() => new()
    {
        Id = "0f1e2d",
        TenantId = "ten_test",
        LocationId = "loc_slc",
        AdvisorUserId = "auth0|advisor",
        FirstName = "Jane",
        Phone = "+18015551234",
        IsSelfEntry = false,
        CreatedAtUtc = Created,
        ConsentCapturedAtUtc = Created,
        SentAtUtc = Created.AddSeconds(1),
        ExpiresAtUtc = Created.AddHours(72),
        RedeemedAtUtc = Created.AddHours(1),
        AcsMessageId = "Outgoing_abc",
        DeliveryStatus = IntakeInviteDeliveryStatus.Delivered,
    };

    [Fact]
    public void ToSummaryDto_ShouldMapTheDialogFields()
    {
        var dto = BuildInvite().ToSummaryDto();

        dto.Id.Should().Be("0f1e2d");
        dto.LocationId.Should().Be("loc_slc");
        dto.FirstName.Should().Be("Jane");
        dto.Phone.Should().Be("+18015551234");
        dto.IsSelfEntry.Should().BeFalse();
        dto.CreatedAtUtc.Should().Be(Created);
        dto.SentAtUtc.Should().Be(Created.AddSeconds(1));
        dto.ExpiresAtUtc.Should().Be(Created.AddHours(72));
        dto.RedeemedAtUtc.Should().Be(Created.AddHours(1));
        dto.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Delivered);
    }

    [Fact]
    public void ToDetailDto_ShouldCarryTheIntakeUrlWhenGiven()
    {
        var dto = BuildInvite().ToDetailDto("https://rvintake.com/slc?src=advisor&inv=x");

        dto.Id.Should().Be("0f1e2d");
        dto.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Delivered);
        dto.IntakeUrl.Should().Be("https://rvintake.com/slc?src=advisor&inv=x");
    }

    [Fact]
    public void ToDetailDto_WithoutAUrl_ShouldLeaveItNull()
    {
        BuildInvite().ToDetailDto().IntakeUrl.Should().BeNull();
    }

    [Fact]
    public void ToDto_ShouldCarryTheSmsEnabledFlag()
    {
        new IntakeInviteCapability(SmsEnabled: true).ToDto().SmsEnabled.Should().BeTrue();
        new IntakeInviteCapability(SmsEnabled: false).ToDto().SmsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ToSummaryDto_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => ((IntakeInvite)null!).ToSummaryDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDetailDto_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => ((IntakeInvite)null!).ToDetailDto();

        act.Should().Throw<ArgumentNullException>();
    }
}
