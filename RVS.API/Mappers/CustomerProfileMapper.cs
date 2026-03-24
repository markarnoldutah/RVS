using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between <see cref="CustomerProfile"/> entities and their DTOs at the API boundary.
/// </summary>
public static class CustomerProfileMapper
{
    /// <summary>
    /// Maps a <see cref="CustomerProfile"/> entity to a <see cref="CustomerProfileDetailResponseDto"/>.
    /// </summary>
    public static CustomerProfileDetailResponseDto ToDetailDto(this CustomerProfile entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CustomerProfileDetailResponseDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Email = entity.Email,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Phone = entity.Phone,
            GlobalCustomerAcctId = entity.GlobalCustomerAcctId,
            TotalRequestCount = entity.TotalRequestCount,
            ServiceRequestIds = entity.ServiceRequestIds,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="CustomerProfile"/> to a <see cref="CustomerInfoDto"/> containing
    /// core contact information. Used when embedding customer info in other responses.
    /// </summary>
    public static CustomerInfoDto ToDto(this CustomerProfile entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CustomerInfoDto
        {
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Email = entity.Email,
            Phone = entity.Phone
        };
    }
}
