namespace RVS.Domain.DTOs
{
    public sealed record UserSessionContextDto
    {
        public string UserId { get; init; } = default!;
        public string DisplayName { get; init; } = default!;
        public string TenantId { get; init; } = default!;
        public string TenantName { get; init; } = default!;
        public List<string> Roles { get; init; } = new();
    }
}
