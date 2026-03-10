namespace RVS.Domain.DTOs
{
    public sealed record LocationSummaryResponseDto
    {
        public string LocationId { get; init; } = default!;
        public string Name { get; init; } = default!;
    }
}
