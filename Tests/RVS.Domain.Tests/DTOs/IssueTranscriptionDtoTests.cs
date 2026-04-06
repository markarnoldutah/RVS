using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Integrations;

namespace RVS.Domain.Tests.DTOs;

public class IssueTranscriptionDtoTests
{
    // ── IssueTranscriptionRequestDto ──────────────────────────────────────

    [Fact]
    public void IssueTranscriptionRequestDto_WhenCreated_SetsAudioBase64()
    {
        var dto = new IssueTranscriptionRequestDto
        {
            AudioBase64 = "SGVsbG8gV29ybGQ=",
            ContentType = "audio/webm"
        };

        dto.AudioBase64.Should().Be("SGVsbG8gV29ybGQ=");
    }

    [Fact]
    public void IssueTranscriptionRequestDto_WhenCreated_SetsContentType()
    {
        var dto = new IssueTranscriptionRequestDto
        {
            AudioBase64 = "SGVsbG8gV29ybGQ=",
            ContentType = "audio/wav"
        };

        dto.ContentType.Should().Be("audio/wav");
    }

    [Fact]
    public void IssueTranscriptionRequestDto_WhenLocaleOmitted_LocaleIsNull()
    {
        var dto = new IssueTranscriptionRequestDto
        {
            AudioBase64 = "SGVsbG8gV29ybGQ=",
            ContentType = "audio/webm"
        };

        dto.Locale.Should().BeNull();
    }

    [Fact]
    public void IssueTranscriptionRequestDto_WhenLocaleProvided_SetsLocale()
    {
        var dto = new IssueTranscriptionRequestDto
        {
            AudioBase64 = "SGVsbG8gV29ybGQ=",
            ContentType = "audio/webm",
            Locale = "es-MX"
        };

        dto.Locale.Should().Be("es-MX");
    }

    [Fact]
    public void IssueTranscriptionRequestDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new IssueTranscriptionRequestDto { AudioBase64 = "data==", ContentType = "audio/webm" };
        var dto2 = new IssueTranscriptionRequestDto { AudioBase64 = "data==", ContentType = "audio/webm" };

        dto1.Should().Be(dto2);
    }

    // ── IssueTranscriptionResultDto ───────────────────────────────────────

    [Fact]
    public void IssueTranscriptionResultDto_WhenTranscriptPresent_SetsRawTranscript()
    {
        var dto = new IssueTranscriptionResultDto
        {
            RawTranscript = "My water heater is not working"
        };

        dto.RawTranscript.Should().Be("My water heater is not working");
    }

    [Fact]
    public void IssueTranscriptionResultDto_WhenCleanedDescriptionPresent_SetsCleanedDescription()
    {
        var dto = new IssueTranscriptionResultDto
        {
            RawTranscript = "um my water heater is not working",
            CleanedDescription = "Water heater is not working."
        };

        dto.CleanedDescription.Should().Be("Water heater is not working.");
    }

    [Fact]
    public void IssueTranscriptionResultDto_WhenNoSpeechDetected_BothFieldsAreNull()
    {
        var dto = new IssueTranscriptionResultDto
        {
            RawTranscript = null,
            CleanedDescription = null
        };

        dto.RawTranscript.Should().BeNull();
        dto.CleanedDescription.Should().BeNull();
    }

    [Fact]
    public void IssueTranscriptionResultDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new IssueTranscriptionResultDto { RawTranscript = "test", CleanedDescription = "Test." };
        var dto2 = new IssueTranscriptionResultDto { RawTranscript = "test", CleanedDescription = "Test." };

        dto1.Should().Be(dto2);
    }

    // ── SpeechToTextResult (domain record) ────────────────────────────────

    [Fact]
    public void SpeechToTextResult_WhenCreated_SetsAllProperties()
    {
        var result = new SpeechToTextResult("My water heater broke", "Water heater is broken.", 0.92, "MockSpeechToTextService");

        result.RawTranscript.Should().Be("My water heater broke");
        result.CleanedDescription.Should().Be("Water heater is broken.");
        result.Confidence.Should().Be(0.92);
        result.Provider.Should().Be("MockSpeechToTextService");
    }

    [Fact]
    public void SpeechToTextResult_WhenCleanedDescriptionIsNull_IsAccepted()
    {
        var result = new SpeechToTextResult("raw text", null, 0.85, "MockSpeechToTextService");

        result.CleanedDescription.Should().BeNull();
    }

    [Fact]
    public void SpeechToTextResult_IsRecord_ValueEqualityByProperties()
    {
        var r1 = new SpeechToTextResult("test", "Test.", 0.9, "Provider");
        var r2 = new SpeechToTextResult("test", "Test.", 0.9, "Provider");

        r1.Should().Be(r2);
    }

    // ── AiOperationResponseDto<IssueTranscriptionResultDto> ───────────────

    [Fact]
    public void AiOperationResponseDto_WithTranscriptionResult_WrapsPayloadCorrectly()
    {
        var dto = new AiOperationResponseDto<IssueTranscriptionResultDto>
        {
            Success = true,
            Result = new IssueTranscriptionResultDto
            {
                RawTranscript = "My battery is dead",
                CleanedDescription = "Battery is dead."
            },
            Confidence = 0.92,
            Warnings = [],
            Provider = "MockSpeechToTextService",
            CorrelationId = "corr-001"
        };

        dto.Success.Should().BeTrue();
        dto.Result!.RawTranscript.Should().Be("My battery is dead");
        dto.Result.CleanedDescription.Should().Be("Battery is dead.");
        dto.Confidence.Should().Be(0.92);
        dto.Provider.Should().Be("MockSpeechToTextService");
    }

    [Fact]
    public void AiOperationResponseDto_WhenTranscriptionFails_HasNullResultAndZeroConfidence()
    {
        var dto = new AiOperationResponseDto<IssueTranscriptionResultDto>
        {
            Success = true,
            Result = null,
            Confidence = 0.0,
            Warnings = ["No speech detected in the provided audio."],
            Provider = "MockSpeechToTextService",
            CorrelationId = "corr-002"
        };

        dto.Result.Should().BeNull();
        dto.Confidence.Should().Be(0.0);
        dto.Warnings.Should().ContainSingle().Which.Should().Be("No speech detected in the provided audio.");
    }
}
