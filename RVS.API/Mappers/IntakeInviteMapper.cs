using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Mappers;

/// <summary>
/// Maps <see cref="IntakeInvite"/> entities to their DTOs at the API boundary
/// (<c>Spec A-14</c>, issue #663). Neither DTO carries the advisor id, the ACS message id or
/// the consent timestamp: the dialog does not need them, and they stay server-side evidence.
/// </summary>
public static class IntakeInviteMapper
{
    /// <summary>
    /// Maps an <see cref="IntakeInvite"/> to an <see cref="IntakeInviteSummaryResponseDto"/>.
    /// </summary>
    public static IntakeInviteSummaryResponseDto ToSummaryDto(this IntakeInvite entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new IntakeInviteSummaryResponseDto
        {
            Id = entity.Id,
            LocationId = entity.LocationId,
            FirstName = entity.FirstName,
            Phone = entity.Phone,
            IsSelfEntry = entity.IsSelfEntry,
            CreatedAtUtc = entity.CreatedAtUtc,
            SentAtUtc = entity.SentAtUtc,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            RedeemedAtUtc = entity.RedeemedAtUtc,
            DeliveryStatus = entity.DeliveryStatus
        };
    }

    /// <summary>
    /// Maps an <see cref="IntakeInviteCapability"/> to its response DTO.
    /// </summary>
    public static IntakeInviteCapabilityResponseDto ToDto(this IntakeInviteCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return new IntakeInviteCapabilityResponseDto { SmsEnabled = capability.SmsEnabled };
    }

    /// <summary>
    /// Maps an <see cref="IntakeInvite"/> to an <see cref="IntakeInviteDetailResponseDto"/>.
    /// </summary>
    /// <param name="entity">The invite.</param>
    /// <param name="intakeUrl">The self-entry intake URL, known only at creation.</param>
    public static IntakeInviteDetailResponseDto ToDetailDto(this IntakeInvite entity, string? intakeUrl = null)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new IntakeInviteDetailResponseDto
        {
            Id = entity.Id,
            LocationId = entity.LocationId,
            FirstName = entity.FirstName,
            Phone = entity.Phone,
            IsSelfEntry = entity.IsSelfEntry,
            CreatedAtUtc = entity.CreatedAtUtc,
            SentAtUtc = entity.SentAtUtc,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            RedeemedAtUtc = entity.RedeemedAtUtc,
            DeliveryStatus = entity.DeliveryStatus,
            IntakeUrl = intakeUrl
        };
    }
}
