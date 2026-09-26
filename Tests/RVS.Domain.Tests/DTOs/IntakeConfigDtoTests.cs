using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class IntakeConfigDtoTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void MaxAttachments_WithinOneToFive_PassesModelValidation(int maxAttachments)
    {
        var dto = new IntakeConfigDto { MaxAttachments = maxAttachments };

        Validate(dto).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(11)]
    public void MaxAttachments_OutsideOneToFive_FailsModelValidation(int maxAttachments)
    {
        var dto = new IntakeConfigDto { MaxAttachments = maxAttachments };

        Validate(dto).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(IntakeConfigDto.MaxAttachments));
    }

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }
}
