using System;
using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record TenantConfigResponseDto
    {
        public string Id { get; init; } = default!;
        public string TenantId { get; init; } = default!;

        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }

        public TenantAccessGateDto AccessGate { get; init; } = new();
    }

    public sealed record TenantAccessGateDto
    {
        public bool LoginsEnabled { get; init; } = true;
        public string? DisabledReason { get; init; }
        public string? DisabledMessage { get; init; }
        
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        public string? SupportContactEmail { get; init; }
        
        public DateTimeOffset? DisabledAtUtc { get; init; }
    }
}
