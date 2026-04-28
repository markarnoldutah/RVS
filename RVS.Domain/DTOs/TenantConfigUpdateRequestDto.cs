using System.Collections.Generic;

namespace RVS.Domain.DTOs
{
    public sealed record TenantConfigUpdateRequestDto
    {
        /// <summary>
        /// When provided, replaces the tenant's entire available capabilities list.
        /// Pass null to leave the existing list unchanged.
        /// </summary>
        public List<TenantCapabilityDto>? AvailableCapabilities { get; init; }
    }
}

