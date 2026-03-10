namespace RVS.Domain.DTOs
{
    public sealed record PracticeContextDto
    {
        public string PracticeId { get; init; } = default!;
        public string Name { get; init; } = default!;
        public List<LocationContextDto> Locations { get; init; } = new();
    }
}
