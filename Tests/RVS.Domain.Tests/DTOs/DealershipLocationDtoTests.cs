using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class DealershipLocationDtoTests
{
    [Fact]
    public void DealershipDetailDto_CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var dto = new DealershipDetailDto
        {
            Id = "dlr-1",
            TenantId = "tenant-1",
            Name = "Acme RV",
            Slug = "acme-rv",
            LogoUrl = "https://cdn.example.com/logo.png",
            ServiceEmail = "service@acmerv.com",
            Phone = "555-1234",
            IntakeConfig = new IntakeConfigDto
            {
                AcceptedFileTypes = [".jpg", ".png"],
                MaxFileSizeMb = 25,
                MaxAttachments = 10,
                AllowAnonymousIntake = true
            },
            CreatedAtUtc = now
        };

        dto.Id.Should().Be("dlr-1");
        dto.Slug.Should().Be("acme-rv");
        dto.IntakeConfig.Should().NotBeNull();
        dto.IntakeConfig!.MaxFileSizeMb.Should().Be(25);
        dto.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void DealershipSummaryDto_CanSetAllProperties()
    {
        var dto = new DealershipSummaryDto
        {
            Id = "dlr-1",
            Name = "Acme RV",
            Slug = "acme-rv",
            Phone = "555-1234"
        };

        dto.Id.Should().Be("dlr-1");
        dto.Name.Should().Be("Acme RV");
        dto.Phone.Should().Be("555-1234");
    }

    [Fact]
    public void LocationDetailDto_CanSetAllProperties()
    {
        var dto = new LocationDetailDto
        {
            Id = "loc-1",
            TenantId = "tenant-1",
            Name = "Main Service Center",
            Slug = "main-service-center",
            Phone = "555-5678",
            Address = new AddressDto
            {
                Address1 = "123 Main St",
                City = "Salt Lake City",
                State = "UT",
                PostalCode = "84101"
            },
            IntakeConfig = new IntakeConfigDto
            {
                MaxFileSizeMb = 50,
                MaxAttachments = 5,
                AllowAnonymousIntake = false
            }
        };

        dto.Address.Should().NotBeNull();
        dto.Address!.City.Should().Be("Salt Lake City");
        dto.IntakeConfig.Should().NotBeNull();
        dto.IntakeConfig!.AllowAnonymousIntake.Should().BeFalse();
    }

    [Fact]
    public void AddressDto_AllFieldsOptional()
    {
        var dto = new AddressDto();

        dto.Address1.Should().BeNull();
        dto.Address2.Should().BeNull();
        dto.City.Should().BeNull();
        dto.State.Should().BeNull();
        dto.PostalCode.Should().BeNull();
    }

    [Fact]
    public void IntakeConfigDto_DefaultCollectionIsEmpty()
    {
        var dto = new IntakeConfigDto();

        dto.AcceptedFileTypes.Should().BeEmpty();
        dto.AiContext.Should().BeNull();
    }
}
