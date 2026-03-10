using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between Practice entities and DTOs at the API boundary
/// </summary>
public static class PracticeMapper
{
    public static PracticeSummaryResponseDto ToSummaryDto(this Practice practice)
    {
        ArgumentNullException.ThrowIfNull(practice);

        return new PracticeSummaryResponseDto
        {
            PracticeId = practice.Id,
            Name = practice.Name,
            IsEnabled = practice.IsEnabled, 
            Locations = practice.Locations?.Select(ToLocationSummaryDto).ToList() ?? []
        };
    }

    public static PracticeDetailResponseDto ToDetailDto(this Practice practice)
    {
        ArgumentNullException.ThrowIfNull(practice);

        return new PracticeDetailResponseDto
        {
            PracticeId = practice.Id,
            Name = practice.Name,
            IsEnabled = practice.IsEnabled, 
            Phone = practice.Phone,
            Email = practice.Email,
            Locations = practice.Locations?.Select(ToLocationSummaryDto).ToList() ?? [],
            // Audit properties
            CreatedAtUtc = practice.CreatedAtUtc,
            UpdatedAtUtc = practice.UpdatedAtUtc,
            CreatedByUserId = practice.CreatedByUserId,
            UpdatedByUserId = practice.UpdatedByUserId
        };
    }

    public static LocationSummaryResponseDto ToLocationSummaryDto(this Location location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return new LocationSummaryResponseDto
        {
            LocationId = location.Id,
            Name = location.Name
        };
    }

    public static List<PracticeSummaryResponseDto> ToSummaryDto(this List<Practice> practices)
    {
        return practices.Select(ToSummaryDto).ToList();
    }
}
