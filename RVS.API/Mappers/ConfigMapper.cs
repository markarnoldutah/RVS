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
            CreatedByUserId = createdByUserId
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

        // Note: UpdatedAtUtc and UpdatedByUserId are set by MarkAsUpdated() in the service layer
    }
}

