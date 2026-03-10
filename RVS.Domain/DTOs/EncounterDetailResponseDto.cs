namespace RVS.Domain.DTOs
{
    public sealed record EncounterDetailResponseDto : EncounterSummaryResponseDto
    {
        public CoverageDecisionResponseDto? CoverageDecision { get; init; }
        public List<EligibilityCheckResponseDto> EligibilityChecks { get; init; } = new();
    }
}
