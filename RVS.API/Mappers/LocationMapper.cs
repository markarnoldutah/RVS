using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between <see cref="Location"/> entities and their DTOs at the API boundary.
/// </summary>
public static class LocationMapper
{
    /// <summary>
    /// Maps a <see cref="Location"/> entity to a <see cref="LocationDetailDto"/>.
    /// </summary>
    public static LocationDetailDto ToDetailDto(this Location entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new LocationDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Slug = entity.Slug,
            Phone = entity.Phone,
            Address = entity.Address.ToDto(),
            IntakeConfig = entity.IntakeConfig.ToDto(),
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="Location"/> entity to a <see cref="LocationSummaryResponseDto"/>.
    /// </summary>
    public static LocationSummaryResponseDto ToSummaryDto(this Location entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new LocationSummaryResponseDto
        {
            LocationId = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            Phone = entity.Phone,
            Address = entity.Address.ToDto(),
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="LocationCreateRequestDto"/> to a new <see cref="Location"/> entity.
    /// When <paramref name="dto"/>.<see cref="LocationCreateRequestDto.Slug"/> is null or
    /// whitespace, a slug is auto-generated from <paramref name="dealershipName"/> and the
    /// location name.
    /// </summary>
    /// <param name="dto">The create request DTO.</param>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="createdByUserId">The ID of the user creating the location.</param>
    /// <param name="dealershipName">Dealership (org) name used for auto-slug generation.</param>
    public static Location ToEntity(this LocationCreateRequestDto dto, string tenantId, string createdByUserId, string? dealershipName = null)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);

        var slug = !string.IsNullOrWhiteSpace(dto.Slug)
            ? dto.Slug.Trim().ToLowerInvariant()
            : SlugGenerator.Generate(dealershipName, dto.Name);

        return new Location
        {
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Name = dto.Name.Trim(),
            Slug = slug,
            Phone = dto.Phone?.Trim(),
            Address = dto.Address is not null ? dto.Address.ToEmbedded() : new AddressEmbedded(),
            IntakeConfig = dto.IntakeConfig is not null ? dto.IntakeConfig.ToEmbedded() : new IntakeFormConfigEmbedded()
        };
    }

    /// <summary>
    /// Applies update values from a <see cref="LocationCreateRequestDto"/> to an existing
    /// <see cref="Location"/> entity, mutating in place.
    /// </summary>
    /// <param name="entity">The location entity to update.</param>
    /// <param name="dto">The request DTO containing updated values.</param>
    /// <param name="updatedByUserId">The ID of the user performing the update.</param>
    public static void ApplyUpdate(this Location entity, LocationCreateRequestDto dto, string updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.Name = dto.Name.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Slug))
        {
            entity.Slug = dto.Slug.Trim().ToLowerInvariant();
        }

        if (dto.Phone is not null)
        {
            entity.Phone = dto.Phone.Trim();
        }

        if (dto.Address is not null)
        {
            entity.Address = dto.Address.ToEmbedded();
        }

        if (dto.IntakeConfig is not null)
        {
            entity.IntakeConfig = dto.IntakeConfig.ToEmbedded();
        }

        entity.MarkAsUpdated(updatedByUserId);
    }

    /// <summary>
    /// Maps an <see cref="AddressEmbedded"/> entity to an <see cref="AddressDto"/>.
    /// </summary>
    public static AddressDto ToDto(this AddressEmbedded address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return new AddressDto
        {
            Address1 = address.Address1,
            Address2 = address.Address2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode
        };
    }

    /// <summary>
    /// Maps an <see cref="AddressDto"/> to an <see cref="AddressEmbedded"/> entity.
    /// </summary>
    public static AddressEmbedded ToEmbedded(this AddressDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new AddressEmbedded
        {
            Address1 = dto.Address1?.Trim(),
            Address2 = dto.Address2?.Trim(),
            City = dto.City?.Trim(),
            State = dto.State?.Trim(),
            PostalCode = dto.PostalCode?.Trim()
        };
    }

    /// <summary>
    /// Maps an <see cref="IntakeFormConfigEmbedded"/> entity to an <see cref="IntakeConfigDto"/>.
    /// </summary>
    public static IntakeConfigDto ToDto(this IntakeFormConfigEmbedded config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new IntakeConfigDto
        {
            AcceptedFileTypes = config.AcceptedFileTypes,
            MaxFileSizeMb = config.MaxFileSizeMb,
            MaxAttachments = config.MaxAttachments,
            AiContext = config.AiContext,
            AllowAnonymousIntake = config.AllowAnonymousIntake
        };
    }

    /// <summary>
    /// Maps an <see cref="IntakeConfigDto"/> to an <see cref="IntakeFormConfigEmbedded"/> entity.
    /// </summary>
    public static IntakeFormConfigEmbedded ToEmbedded(this IntakeConfigDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new IntakeFormConfigEmbedded
        {
            AcceptedFileTypes = dto.AcceptedFileTypes,
            MaxFileSizeMb = dto.MaxFileSizeMb,
            MaxAttachments = dto.MaxAttachments,
            AiContext = dto.AiContext?.Trim(),
            AllowAnonymousIntake = dto.AllowAnonymousIntake
        };
    }
}
