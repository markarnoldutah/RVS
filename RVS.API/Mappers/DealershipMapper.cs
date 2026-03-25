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

    /// <summary>
    /// Applies update values from a <see cref="DealershipUpdateRequestDto"/> to an existing
    /// <see cref="Dealership"/> entity, mutating in place.
    /// </summary>
    /// <param name="entity">The dealership entity to update.</param>
    /// <param name="dto">The request DTO containing updated values.</param>
    /// <param name="updatedByUserId">The ID of the user performing the update.</param>
    public static void ApplyUpdate(this Dealership entity, DealershipUpdateRequestDto dto, string? updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.Name = dto.Name.Trim();
        entity.Slug = dto.Slug.Trim().ToLowerInvariant();
        entity.LogoUrl = dto.LogoUrl?.Trim();
        entity.ServiceEmail = dto.ServiceEmail?.Trim();
        entity.Phone = dto.Phone?.Trim();

        if (dto.IntakeConfig is not null)
        {
            entity.IntakeConfig = dto.IntakeConfig.ToEmbedded();
        }

        entity.MarkAsUpdated(updatedByUserId);
    }
}
