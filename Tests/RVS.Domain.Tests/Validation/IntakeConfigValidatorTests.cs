using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class IntakeConfigValidatorTests
{
    [Fact]
    public void Validate_DefaultConfig_ReturnsSuccess()
    {
        var result = IntakeConfigValidator.Validate(new IntakeFormConfigEmbedded());

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Validate_MaxAttachmentsWithinOneToFive_ReturnsSuccess(int maxAttachments)
    {
        var config = new IntakeFormConfigEmbedded { MaxAttachments = maxAttachments };

        var result = IntakeConfigValidator.Validate(config);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(11)]
    public void Validate_MaxAttachmentsOutsideOneToFive_ReturnsFailure(int maxAttachments)
    {
        var config = new IntakeFormConfigEmbedded { MaxAttachments = maxAttachments };

        var result = IntakeConfigValidator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("between 1 and 5");
    }

    [Fact]
    public void Validate_NullConfig_ThrowsArgumentNullException()
    {
        var act = () => IntakeConfigValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    [InlineData(0, 5)]
    [InlineData(-2, 5)]
    public void EffectiveAttachmentCap_ClampsStoredValueToPlatformBounds(int stored, int expected)
    {
        IntakeConfigValidator.EffectiveAttachmentCap(stored).Should().Be(expected);
    }

    [Fact]
    public void EffectiveAttachmentCap_NoConfiguredValue_ReturnsPlatformMaximum()
    {
        IntakeConfigValidator.EffectiveAttachmentCap(null).Should().Be(5);
    }
}
