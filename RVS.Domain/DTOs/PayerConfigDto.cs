namespace RVS.Domain.DTOs
{
    public sealed record PayerConfigDto
    {
        public string PayerId { get; init; } = default!;
        public string? DisplayNameOverride { get; init; }
        public bool IsEnabled { get; init; }
        public string? Notes { get; init; }
    }
}
