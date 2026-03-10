namespace RVS.Domain.DTOs
{
    public sealed record UserSessionContextDto
    {
        public string UserId { get; init; } = default!;
        public string DisplayName { get; init; } = default!;
        public string TenantId { get; init; } = default!;
        public string TenantName { get; init; } = default!;
        public List<PracticeContextDto> Practices { get; init; } = new();
        public List<string> Roles { get; init; } = new();  // TODO should this be an IReadOnly List as in TenantAccessRepository? (see SessionService)
    }
}
