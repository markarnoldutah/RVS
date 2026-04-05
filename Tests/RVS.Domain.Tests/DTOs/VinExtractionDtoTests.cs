using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Integrations;

namespace RVS.Domain.Tests.DTOs;

public class VinExtractionDtoTests
{
    // ── VinExtractionRequestDto ────────────────────────────────────────────

    [Fact]
    public void VinExtractionRequestDto_WhenCreated_SetsImageBase64()
    {
        var dto = new VinExtractionRequestDto
        {
            ImageBase64 = "abc123base64==",
            ContentType = "image/jpeg"
        };

        dto.ImageBase64.Should().Be("abc123base64==");
    }

    [Fact]
    public void VinExtractionRequestDto_WhenCreated_SetsContentType()
    {
        var dto = new VinExtractionRequestDto
        {
            ImageBase64 = "abc123base64==",
            ContentType = "image/png"
        };

        dto.ContentType.Should().Be("image/png");
    }

    [Fact]
    public void VinExtractionRequestDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new VinExtractionRequestDto { ImageBase64 = "data==", ContentType = "image/jpeg" };
        var dto2 = new VinExtractionRequestDto { ImageBase64 = "data==", ContentType = "image/jpeg" };

        dto1.Should().Be(dto2);
    }

    // ── VinExtractionResultDto ─────────────────────────────────────────────

    [Fact]
    public void VinExtractionResultDto_WhenVinPresent_SetsVin()
    {
        var dto = new VinExtractionResultDto { Vin = "1RGDE4428R1000001" };

        dto.Vin.Should().Be("1RGDE4428R1000001");
    }

    [Fact]
    public void VinExtractionResultDto_WhenVinNotDetected_VinIsNull()
    {
        var dto = new VinExtractionResultDto { Vin = null };

        dto.Vin.Should().BeNull();
    }

    [Fact]
    public void VinExtractionResultDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new VinExtractionResultDto { Vin = "1RGDE4428R1000001" };
        var dto2 = new VinExtractionResultDto { Vin = "1RGDE4428R1000001" };

        dto1.Should().Be(dto2);
    }

    // ── VinExtractionResult (domain record) ───────────────────────────────

    [Fact]
    public void VinExtractionResult_WhenCreated_SetsAllProperties()
    {
        var result = new VinExtractionResult("1RGDE4428R1000001", 0.95, "MockVinExtractionService");

        result.Vin.Should().Be("1RGDE4428R1000001");
        result.Confidence.Should().Be(0.95);
        result.Provider.Should().Be("MockVinExtractionService");
    }

    [Fact]
    public void VinExtractionResult_IsRecord_ValueEqualityByProperties()
    {
        var r1 = new VinExtractionResult("1RGDE4428R1000001", 0.95, "MockVinExtractionService");
        var r2 = new VinExtractionResult("1RGDE4428R1000001", 0.95, "MockVinExtractionService");

        r1.Should().Be(r2);
    }

    // ── AiOperationResponseDto<VinExtractionResultDto> ────────────────────

    [Fact]
    public void AiOperationResponseDto_WithVinExtractionResult_WrapsPayloadCorrectly()
    {
        var dto = new AiOperationResponseDto<VinExtractionResultDto>
        {
            Success = true,
            Result = new VinExtractionResultDto { Vin = "1RGDE4428R1000001" },
            Confidence = 0.95,
            Warnings = [],
            Provider = "MockVinExtractionService",
            CorrelationId = "corr-001"
        };

        dto.Success.Should().BeTrue();
        dto.Result!.Vin.Should().Be("1RGDE4428R1000001");
        dto.Confidence.Should().Be(0.95);
        dto.Provider.Should().Be("MockVinExtractionService");
    }

    [Fact]
    public void AiOperationResponseDto_WhenExtractionFails_HasNullResultAndZeroConfidence()
    {
        var dto = new AiOperationResponseDto<VinExtractionResultDto>
        {
            Success = true,
            Result = null,
            Confidence = 0.0,
            Warnings = ["No VIN detected in the provided image."],
            Provider = "MockVinExtractionService",
            CorrelationId = "corr-002"
        };

        dto.Result.Should().BeNull();
        dto.Confidence.Should().Be(0.0);
        dto.Warnings.Should().ContainSingle().Which.Should().Be("No VIN detected in the provided image.");
    }
}
