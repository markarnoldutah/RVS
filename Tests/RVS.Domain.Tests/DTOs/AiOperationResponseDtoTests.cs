using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class AiOperationResponseDtoTests
{
    [Fact]
    public void AiOperationResponseDto_WhenSuccessWithResult_SetsAllProperties()
    {
        var dto = new AiOperationResponseDto<string>
        {
            Success = true,
            Result = "1HGBH41JXMN109186",
            Confidence = 0.95,
            Warnings = [],
            Provider = "AzureOpenAiVinExtractionService",
            CorrelationId = "corr-abc-123"
        };

        dto.Success.Should().BeTrue();
        dto.Result.Should().Be("1HGBH41JXMN109186");
        dto.Confidence.Should().Be(0.95);
        dto.Warnings.Should().BeEmpty();
        dto.Provider.Should().Be("AzureOpenAiVinExtractionService");
        dto.CorrelationId.Should().Be("corr-abc-123");
    }

    [Fact]
    public void AiOperationResponseDto_WhenSuccessWithNullResult_IsValid()
    {
        var dto = new AiOperationResponseDto<string>
        {
            Success = true,
            Result = null,
            Confidence = 0.0,
            Warnings = ["No VIN detected in the provided image."],
            Provider = "MockVinExtractionService",
            CorrelationId = "corr-xyz-456"
        };

        dto.Success.Should().BeTrue();
        dto.Result.Should().BeNull();
        dto.Confidence.Should().Be(0.0);
        dto.Warnings.Should().ContainSingle().Which.Should().Be("No VIN detected in the provided image.");
        dto.Provider.Should().Be("MockVinExtractionService");
    }

    [Fact]
    public void AiOperationResponseDto_WhenFailure_SuccessIsFalse()
    {
        var dto = new AiOperationResponseDto<string>
        {
            Success = false,
            Result = null,
            Confidence = 0.0,
            Warnings = [],
            Provider = "AzureOpenAiVinExtractionService",
            CorrelationId = "corr-fail-789"
        };

        dto.Success.Should().BeFalse();
        dto.Result.Should().BeNull();
        dto.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void AiOperationResponseDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new AiOperationResponseDto<int>
        {
            Success = true,
            Result = 42,
            Confidence = 1.0,
            Warnings = [],
            Provider = "TestProvider",
            CorrelationId = "id-1"
        };

        var dto2 = new AiOperationResponseDto<int>
        {
            Success = true,
            Result = 42,
            Confidence = 1.0,
            Warnings = [],
            Provider = "TestProvider",
            CorrelationId = "id-1"
        };

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void AiOperationResponseDto_ConfidenceRange_AcceptsZeroToOne()
    {
        var low = new AiOperationResponseDto<string>
        {
            Success = true,
            Result = null,
            Confidence = 0.0,
            Warnings = [],
            Provider = "Provider",
            CorrelationId = "id"
        };

        var high = new AiOperationResponseDto<string>
        {
            Success = true,
            Result = "VIN",
            Confidence = 1.0,
            Warnings = [],
            Provider = "Provider",
            CorrelationId = "id"
        };

        low.Confidence.Should().Be(0.0);
        high.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void AiOperationResponseDto_Warnings_CanContainMultipleMessages()
    {
        var dto = new AiOperationResponseDto<string>
        {
            Success = true,
            Result = null,
            Confidence = 0.3,
            Warnings = ["Image resolution too low.", "Partial VIN match only."],
            Provider = "AzureOpenAiVinExtractionService",
            CorrelationId = "corr-warn-001"
        };

        dto.Warnings.Should().HaveCount(2);
        dto.Warnings.Should().Contain("Image resolution too low.");
        dto.Warnings.Should().Contain("Partial VIN match only.");
    }
}
