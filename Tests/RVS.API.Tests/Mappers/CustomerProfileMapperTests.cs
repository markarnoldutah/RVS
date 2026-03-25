using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class CustomerProfileMapperTests
{
    // ── ToDetailDto ──────────────────────────────────────────────────────────

    [Fact]
    public void ToDetailDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        CustomerProfile? entity = null;

        var act = () => entity!.ToDetailDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDetailDto_ShouldMapAllFields()
    {
        var now = DateTime.UtcNow;
        var entity = new CustomerProfile
        {
            TenantId = "ten_1",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Phone = "(801) 555-1234",
            GlobalCustomerAcctId = "gca_1",
            TotalRequestCount = 3,
            ServiceRequestIds = ["sr_001", "sr_002", "sr_003"],
            CreatedAtUtc = now
        };

        var dto = entity.ToDetailDto();

        dto.Id.Should().Be(entity.Id);
        dto.TenantId.Should().Be("ten_1");
        dto.Email.Should().Be("jane@example.com");
        dto.FirstName.Should().Be("Jane");
        dto.LastName.Should().Be("Doe");
        dto.Phone.Should().Be("(801) 555-1234");
        dto.GlobalCustomerAcctId.Should().Be("gca_1");
        dto.TotalRequestCount.Should().Be(3);
        dto.ServiceRequestIds.Should().HaveCount(3);
        dto.CreatedAtUtc.Should().Be(now);
        dto.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenPhoneIsNull_ShouldMapAsNull()
    {
        var entity = new CustomerProfile
        {
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Phone = null
        };

        var dto = entity.ToDetailDto();

        dto.Phone.Should().BeNull();
    }

    // ── ToDto (CustomerInfoDto) ──────────────────────────────────────────────

    [Fact]
    public void ToDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        CustomerProfile? entity = null;

        var act = () => entity!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDto_ShouldMapContactFields()
    {
        var entity = new CustomerProfile
        {
            Email = "mike@example.com",
            FirstName = "Mike",
            LastName = "Johnson",
            Phone = "(801) 555-9999"
        };

        var dto = entity.ToDto();

        dto.Should().BeOfType<CustomerInfoDto>();
        dto.FirstName.Should().Be("Mike");
        dto.LastName.Should().Be("Johnson");
        dto.Email.Should().Be("mike@example.com");
        dto.Phone.Should().Be("(801) 555-9999");
    }

    [Fact]
    public void ToDto_WhenPhoneIsNull_ShouldMapAsNull()
    {
        var entity = new CustomerProfile
        {
            Email = "mike@example.com",
            FirstName = "Mike",
            LastName = "Johnson"
        };

        var dto = entity.ToDto();

        dto.Phone.Should().BeNull();
    }
}
