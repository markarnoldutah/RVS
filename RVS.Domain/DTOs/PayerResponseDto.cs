namespace RVS.Domain.DTOs
{
    public sealed record PayerResponseDto
    {
        public string PayerId { get; init; } = default!;
        public string Name { get; init; } = default!;
        public List<string> SupportedPlanTypes { get; init; } = new();
        public string? AvailityPayerCode { get; init; }
        public string? X12PayerId { get; init; }
        public bool IsMedicare { get; init; }
        public bool IsMedicaid { get; init; }

        // Audit properties (payers are mostly static reference data)
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
