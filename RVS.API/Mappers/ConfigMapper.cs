using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between Config entities and DTOs at the API boundary.
/// </summary>
public static class ConfigMapper
{
    /// <summary>
    /// Maps a <see cref="TenantConfigCreateRequestDto"/> to a <see cref="TenantConfig"/> entity.
    /// </summary>
    public static TenantConfig ToEntity(this TenantConfigCreateRequestDto dto, string tenantId, string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return new TenantConfig
        {
            Id = $"{tenantId}_config",
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
            AvailableCapabilities = DefaultCapabilities()
        };
    }

    /// <summary>
    /// Maps a <see cref="TenantConfig"/> entity to a <see cref="TenantConfigResponseDto"/>.
    /// </summary>
    public static TenantConfigResponseDto ToDto(this TenantConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new TenantConfigResponseDto
        {
            Id = config.Id,
            TenantId = config.TenantId,

            AccessGate = new TenantAccessGateDto
            {
                LoginsEnabled = config.AccessGate?.LoginsEnabled ?? true,
                DisabledReason = config.AccessGate?.DisabledReason,
                DisabledMessage = config.AccessGate?.DisabledMessage,
                SupportContactEmail = config.AccessGate?.SupportContactEmail,
                DisabledAtUtc = config.AccessGate?.DisabledAtUtc
            },

            AvailableCapabilities = config.AvailableCapabilities
                .Select(c => new TenantCapabilityDto
                {
                    Code = c.Code,
                    Name = c.Name,
                    Description = c.Description,
                    SortOrder = c.SortOrder,
                    IsActive = c.IsActive
                })
                .ToList(),

            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = config.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Applies update DTO values to an existing <see cref="TenantConfig"/> entity.
    /// </summary>
    public static void ApplyUpdateFromDto(this TenantConfig entity, TenantConfigUpdateRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.AvailableCapabilities is not null)
        {
            entity.AvailableCapabilities = dto.AvailableCapabilities
                .Select(c => new TenantCapabilityEmbedded
                {
                    Code = c.Code.Trim().ToLowerInvariant(),
                    Name = c.Name.Trim(),
                    Description = c.Description?.Trim(),
                    SortOrder = c.SortOrder,
                    IsActive = c.IsActive
                })
                .ToList();
        }

        // Note: UpdatedAtUtc and UpdatedByUserId are set by MarkAsUpdated() in the service layer
    }

    /// <summary>
    /// Returns the default starter list of RV service capabilities seeded into every new tenant config.
    /// </summary>
    public static List<TenantCapabilityEmbedded> DefaultCapabilities() =>
    [
        new() { Code = "diesel-service",    Name = "Diesel Engine Service",         SortOrder = 10 },
        new() { Code = "body-repair",       Name = "Body & Collision Repair",        SortOrder = 20 },
        new() { Code = "rv-refrigerator",   Name = "RV Refrigerator Service",        SortOrder = 30 },
        new() { Code = "slide-out-repair",  Name = "Slide-Out Repair & Leveling",   SortOrder = 40 },
        new() { Code = "roof-repair",       Name = "Roof Repair & Resealing",        SortOrder = 50 },
        new() { Code = "electrical",        Name = "Electrical Systems",             SortOrder = 60 },
        new() { Code = "plumbing",          Name = "Plumbing & Water Systems",       SortOrder = 70 },
        new() { Code = "hvac",              Name = "HVAC (Heating & Cooling)",       SortOrder = 80 },
        new() { Code = "generator",         Name = "Generator Service",              SortOrder = 90 },
        new() { Code = "warranty-service",  Name = "Warranty Work",                 SortOrder = 100 },
        new() { Code = "mobile-service",    Name = "Mobile / On-Site Service",       SortOrder = 110 },
        new() { Code = "winterization",     Name = "Winterization & De-Winterization", SortOrder = 120 },
        new() { Code = "safety-inspection", Name = "Safety Inspection",              SortOrder = 130 },
        new() { Code = "tire-service",      Name = "Tire & Wheel Service",           SortOrder = 140 },
    ];
}

