namespace RVS.Domain.DTOs
{
    public record PracticeSummaryResponseDto
    {
        public string PracticeId { get; init; } = default!;
        public string Name { get; init; } = default!;
        public bool IsEnabled { get; init; }  
        public List<LocationSummaryResponseDto> Locations { get; init; } = new();
    }
}
