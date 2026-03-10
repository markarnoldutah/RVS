namespace RVS.Domain.DTOs
{
    public sealed record CoverageDecisionResponseDto
    {
        public string EncounterCoverageDecisionId { get; init; } = default!;
        public string PrimaryCoverageEnrollmentId { get; init; } = default!;
        public string? SecondaryCoverageEnrollmentId { get; init; }
        public string CobReason { get; init; } = default!;
        public string? CobDeterminationSource { get; init; }
        public bool OverriddenByUser { get; init; }
        public string? OverrideNote { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public string? CreatedByUserId { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
