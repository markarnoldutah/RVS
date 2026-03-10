namespace RVS.Domain.DTOs
{
    public sealed record EligibilityCheckResponseDto : EligibilityCheckSummaryResponseDto
    {
        public string? RawStatusCode { get; init; }
        public string? RawStatusDescription { get; init; }
        public string MemberIdSnapshot { get; init; } = default!;
        public string? GroupNumberSnapshot { get; init; }
        public string? PlanNameSnapshot { get; init; }
        public DateTime? EffectiveDateSnapshot { get; init; }
        public DateTime? TerminationDateSnapshot { get; init; }
        public string? ErrorMessage { get; init; }
        
        /// <summary>
        /// Validation messages from payer (populated on request errors).
        /// </summary>
        public List<string>? ValidationMessages { get; init; }
        
        public List<CoverageLineResponseDto> CoverageLines { get; init; } = new();
    }
}
