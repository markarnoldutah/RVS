using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between Payer entities and DTOs at the API boundary
/// </summary>
public static class PayerMapper
{
    public static PayerResponseDto ToDto(this Payer payer)
    {
        ArgumentNullException.ThrowIfNull(payer);

        return new PayerResponseDto
        {
            PayerId = payer.Id,
            Name = payer.Name,
            SupportedPlanTypes = payer.SupportedPlanTypes ?? new List<string>(),
            AvailityPayerCode = payer.AvailityPayerCode,
            X12PayerId = payer.X12PayerId,
            IsMedicare = payer.IsMedicare,
            IsMedicaid = payer.IsMedicaid,
            // Audit properties
            CreatedAtUtc = payer.CreatedAtUtc,
            UpdatedAtUtc = payer.UpdatedAtUtc,
            CreatedByUserId = payer.CreatedByUserId,
            UpdatedByUserId = payer.UpdatedByUserId
        };
    }

    public static List<PayerResponseDto> ToDto(this List<Payer> payers)
    {
        return payers.Select(ToDto).ToList();
    }
}
