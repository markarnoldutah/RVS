using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class AiEnrichmentMetadataDtoTests
{
    [Fact]
    public void AiEnrichmentMetadataDto_WhenFullyPopulated_SetsAllProperties()
    {
        var enrichedAt = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc);

        var dto = new AiEnrichmentMetadataDto
        {
            CategorySuggestionProvider = "AzureOpenAiIssueTextRefinementService",
            CategorySuggestionConfidence = 0.91,
            DiagnosticQuestionsProvider = "AzureOpenAiCategorizationService",
            TranscriptionProvider = "AzureWhisperSpeechToTextService",
            TranscriptionConfidence = 0.88,
            VinExtractionProvider = "AzureOpenAiVinExtractionService",
            VinExtractionConfidence = 0.95,
            InsightsSuggestionProvider = "AzureOpenAiIssueTextRefinementService",
            InsightsSuggestionConfidence = 0.82,
            EnrichedAtUtc = enrichedAt
        };

        dto.CategorySuggestionProvider.Should().Be("AzureOpenAiIssueTextRefinementService");
        dto.CategorySuggestionConfidence.Should().Be(0.91);
        dto.DiagnosticQuestionsProvider.Should().Be("AzureOpenAiCategorizationService");
        dto.TranscriptionProvider.Should().Be("AzureWhisperSpeechToTextService");
        dto.TranscriptionConfidence.Should().Be(0.88);
        dto.VinExtractionProvider.Should().Be("AzureOpenAiVinExtractionService");
        dto.VinExtractionConfidence.Should().Be(0.95);
        dto.InsightsSuggestionProvider.Should().Be("AzureOpenAiIssueTextRefinementService");
        dto.InsightsSuggestionConfidence.Should().Be(0.82);
        dto.EnrichedAtUtc.Should().Be(enrichedAt);
    }

    [Fact]
    public void AiEnrichmentMetadataDto_WhenAllNulls_DefaultsToNull()
    {
        var dto = new AiEnrichmentMetadataDto();

        dto.CategorySuggestionProvider.Should().BeNull();
        dto.CategorySuggestionConfidence.Should().BeNull();
        dto.DiagnosticQuestionsProvider.Should().BeNull();
        dto.TranscriptionProvider.Should().BeNull();
        dto.TranscriptionConfidence.Should().BeNull();
        dto.VinExtractionProvider.Should().BeNull();
        dto.VinExtractionConfidence.Should().BeNull();
        dto.InsightsSuggestionProvider.Should().BeNull();
        dto.InsightsSuggestionConfidence.Should().BeNull();
        dto.EnrichedAtUtc.Should().BeNull();
    }

    [Fact]
    public void AiEnrichmentMetadataDto_WhenPartiallyPopulated_OnlySetFieldsHaveValues()
    {
        var dto = new AiEnrichmentMetadataDto
        {
            VinExtractionProvider = "MockVinExtractionService",
            VinExtractionConfidence = 0.95,
            EnrichedAtUtc = DateTime.UtcNow
        };

        dto.VinExtractionProvider.Should().Be("MockVinExtractionService");
        dto.VinExtractionConfidence.Should().Be(0.95);
        dto.EnrichedAtUtc.Should().NotBeNull();
        dto.CategorySuggestionProvider.Should().BeNull();
        dto.TranscriptionProvider.Should().BeNull();
        dto.InsightsSuggestionProvider.Should().BeNull();
    }

    [Fact]
    public void AiEnrichmentMetadataDto_IsRecord_SupportsValueEquality()
    {
        var enrichedAt = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc);

        var dto1 = new AiEnrichmentMetadataDto
        {
            VinExtractionProvider = "MockVinExtractionService",
            VinExtractionConfidence = 0.95,
            EnrichedAtUtc = enrichedAt
        };

        var dto2 = new AiEnrichmentMetadataDto
        {
            VinExtractionProvider = "MockVinExtractionService",
            VinExtractionConfidence = 0.95,
            EnrichedAtUtc = enrichedAt
        };

        dto1.Should().Be(dto2);
    }
}
