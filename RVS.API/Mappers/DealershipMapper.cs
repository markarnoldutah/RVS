using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between <see cref="Dealership"/> entities and their DTOs at the API boundary.
/// </summary>
public static class DealershipMapper
{
    /// <summary>
    /// Maps a <see cref="Dealership"/> entity to a <see cref="DealershipDetailDto"/>.
    /// </summary>
    public static DealershipDetailDto ToDetailDto(this Dealership entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new DealershipDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Slug = entity.Slug,
            LogoUrl = entity.LogoUrl,
            ServiceEmail = entity.ServiceEmail,
            Phone = entity.Phone,
            IntakeConfig = entity.IntakeConfig.ToDto(),
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="Dealership"/> entity to a <see cref="DealershipSummaryDto"/>.
    /// </summary>
    public static DealershipSummaryDto ToSummaryDto(this Dealership entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new DealershipSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            Phone = entity.Phone
        };
    }
}
