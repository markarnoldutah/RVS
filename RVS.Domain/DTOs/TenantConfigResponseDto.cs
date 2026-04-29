using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Master list of service capabilities configured for this tenant.
        /// </summary>
        public List<TenantCapabilityDto> AvailableCapabilities { get; init; } = [];
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

    /// <summary>
    /// A service capability entry returned in tenant configuration.
    /// </summary>
    public sealed record TenantCapabilityDto
    {
        public string Code { get; init; } = default!;
        public string Name { get; init; } = default!;
        public string? Description { get; init; }
        public int SortOrder { get; init; }
        public bool IsActive { get; init; } = true;
    }
}
