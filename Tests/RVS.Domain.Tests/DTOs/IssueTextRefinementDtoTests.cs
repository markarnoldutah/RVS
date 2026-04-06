using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Integrations;

namespace RVS.Domain.Tests.DTOs;

public class IssueTextRefinementDtoTests
{
    // ── IssueTextRefinementRequestDto ─────────────────────────────────────

    [Fact]
    public void IssueTextRefinementRequestDto_WhenCreated_SetsRawTranscript()
    {
        var dto = new IssueTextRefinementRequestDto
        {
            RawTranscript = "um my water heater stopped working yesterday"
        };

        dto.RawTranscript.Should().Be("um my water heater stopped working yesterday");
    }

    [Fact]
    public void IssueTextRefinementRequestDto_WhenIssueCategoryProvided_SetsIssueCategory()
    {
        var dto = new IssueTextRefinementRequestDto
        {
            RawTranscript = "water heater issue",
            IssueCategory = "Plumbing"
        };

        dto.IssueCategory.Should().Be("Plumbing");
    }

    [Fact]
    public void IssueTextRefinementRequestDto_WhenIssueCategoryOmitted_IsNull()
    {
        var dto = new IssueTextRefinementRequestDto
        {
            RawTranscript = "water heater issue"
        };

        dto.IssueCategory.Should().BeNull();
    }

    [Fact]
    public void IssueTextRefinementRequestDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new IssueTextRefinementRequestDto { RawTranscript = "test", IssueCategory = "Electrical" };
        var dto2 = new IssueTextRefinementRequestDto { RawTranscript = "test", IssueCategory = "Electrical" };

        dto1.Should().Be(dto2);
    }

    // ── IssueTextRefinementResultDto ──────────────────────────────────────

    [Fact]
    public void IssueTextRefinementResultDto_WhenCleanedDescriptionPresent_SetsCleanedDescription()
    {
        var dto = new IssueTextRefinementResultDto
        {
            CleanedDescription = "Water heater stopped working yesterday."
        };

        dto.CleanedDescription.Should().Be("Water heater stopped working yesterday.");
    }

    [Fact]
    public void IssueTextRefinementResultDto_WhenRefinementFails_CleanedDescriptionIsNull()
    {
        var dto = new IssueTextRefinementResultDto
        {
            CleanedDescription = null
        };

        dto.CleanedDescription.Should().BeNull();
    }

    [Fact]
    public void IssueTextRefinementResultDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new IssueTextRefinementResultDto { CleanedDescription = "Cleaned text." };
        var dto2 = new IssueTextRefinementResultDto { CleanedDescription = "Cleaned text." };

        dto1.Should().Be(dto2);
    }

    // ── IssueTextRefinementResult (domain record) ─────────────────────────

    [Fact]
    public void IssueTextRefinementResult_WhenCreated_SetsAllProperties()
    {
        var result = new IssueTextRefinementResult("Water heater is broken.", 0.88, "RuleBasedIssueTextRefinementService");

        result.CleanedDescription.Should().Be("Water heater is broken.");
        result.Confidence.Should().Be(0.88);
        result.Provider.Should().Be("RuleBasedIssueTextRefinementService");
    }

    [Fact]
    public void IssueTextRefinementResult_IsRecord_ValueEqualityByProperties()
    {
        var r1 = new IssueTextRefinementResult("Cleaned.", 0.9, "Provider");
        var r2 = new IssueTextRefinementResult("Cleaned.", 0.9, "Provider");

        r1.Should().Be(r2);
    }

    // ── IssueCategorySuggestionRequestDto ─────────────────────────────────

    [Fact]
    public void IssueCategorySuggestionRequestDto_WhenCreated_SetsIssueDescription()
    {
        var dto = new IssueCategorySuggestionRequestDto
        {
            IssueDescription = "The air conditioning is blowing hot air."
        };

        dto.IssueDescription.Should().Be("The air conditioning is blowing hot air.");
    }

    [Fact]
    public void IssueCategorySuggestionRequestDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new IssueCategorySuggestionRequestDto { IssueDescription = "AC problem" };
        var dto2 = new IssueCategorySuggestionRequestDto { IssueDescription = "AC problem" };

        dto1.Should().Be(dto2);
    }

    // ── IssueCategorySuggestionResultDto ──────────────────────────────────

    [Fact]
    public void IssueCategorySuggestionResultDto_WhenCategoryPresent_SetsIssueCategory()
    {
        var dto = new IssueCategorySuggestionResultDto
        {
            IssueCategory = "HVAC"
        };

        dto.IssueCategory.Should().Be("HVAC");
    }

    [Fact]
    public void IssueCategorySuggestionResultDto_WhenNoConfidentSuggestion_IssueCategoryIsNull()
    {
        var dto = new IssueCategorySuggestionResultDto
        {
            IssueCategory = null
        };

        dto.IssueCategory.Should().BeNull();
    }

    [Fact]
    public void IssueCategorySuggestionResultDto_IsRecord_ValueEqualityByProperties()
    {
        var dto1 = new IssueCategorySuggestionResultDto { IssueCategory = "Electrical" };
        var dto2 = new IssueCategorySuggestionResultDto { IssueCategory = "Electrical" };

        dto1.Should().Be(dto2);
    }

    // ── IssueCategorySuggestionResult (domain record) ─────────────────────

    [Fact]
    public void IssueCategorySuggestionResult_WhenCreated_SetsAllProperties()
    {
        var result = new IssueCategorySuggestionResult("HVAC", 0.85, "RuleBasedIssueTextRefinementService");

        result.IssueCategory.Should().Be("HVAC");
        result.Confidence.Should().Be(0.85);
        result.Provider.Should().Be("RuleBasedIssueTextRefinementService");
    }

    [Fact]
    public void IssueCategorySuggestionResult_WhenNoCategoryAvailable_IsNull()
    {
        var result = new IssueCategorySuggestionResult(null, 0.0, "RuleBasedIssueTextRefinementService");

        result.IssueCategory.Should().BeNull();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void IssueCategorySuggestionResult_IsRecord_ValueEqualityByProperties()
    {
        var r1 = new IssueCategorySuggestionResult("Electrical", 0.9, "Provider");
        var r2 = new IssueCategorySuggestionResult("Electrical", 0.9, "Provider");

        r1.Should().Be(r2);
    }

    // ── AiOperationResponseDto wrapping ───────────────────────────────────

    [Fact]
    public void AiOperationResponseDto_WithRefinementResult_WrapsPayloadCorrectly()
    {
        var dto = new AiOperationResponseDto<IssueTextRefinementResultDto>
        {
            Success = true,
            Result = new IssueTextRefinementResultDto { CleanedDescription = "Water heater broken." },
            Confidence = 0.88,
            Warnings = [],
            Provider = "RuleBasedIssueTextRefinementService",
            CorrelationId = "corr-003"
        };

        dto.Success.Should().BeTrue();
        dto.Result!.CleanedDescription.Should().Be("Water heater broken.");
        dto.Confidence.Should().Be(0.88);
        dto.Provider.Should().Be("RuleBasedIssueTextRefinementService");
    }

    [Fact]
    public void AiOperationResponseDto_WithCategorySuggestionResult_WrapsPayloadCorrectly()
    {
        var dto = new AiOperationResponseDto<IssueCategorySuggestionResultDto>
        {
            Success = true,
            Result = new IssueCategorySuggestionResultDto { IssueCategory = "HVAC" },
            Confidence = 0.85,
            Warnings = [],
            Provider = "RuleBasedIssueTextRefinementService",
            CorrelationId = "corr-004"
        };

        dto.Success.Should().BeTrue();
        dto.Result!.IssueCategory.Should().Be("HVAC");
        dto.Confidence.Should().Be(0.85);
    }
}
