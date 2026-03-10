namespace RVS.Domain.DTOs
{
    public sealed record CoverageLineResponseDto
    {
        public string CoverageLineId { get; init; } = default!;
        public string ServiceTypeCode { get; init; } = default!;
        public string? CoverageDescription { get; init; }
        public decimal? CopayAmount { get; init; }
        public decimal? CoinsurancePercent { get; init; }
        public decimal? DeductibleAmount { get; init; }
        public decimal? RemainingDeductible { get; init; }
        public decimal? OutOfPocketMax { get; init; }
        public decimal? RemainingOutOfPocket { get; init; }
        public decimal? AllowanceAmount { get; init; }
        public string? NetworkIndicator { get; init; }
        public DateTime? EffectiveDate { get; init; }
        public DateTime? TerminationDate { get; init; }
        public string? AdditionalInfo { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
