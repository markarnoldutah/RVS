namespace RVS.Domain.DTOs
{
    public sealed record LocationContextDto
    {
        public string LocationId { get; init; } = default!;
        public string Name { get; init; } = default!;
    }
}
