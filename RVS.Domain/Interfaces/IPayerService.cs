using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces
{
    public interface IPayerService
    {
        // Payer lookup and search
        Task<List<Payer>> SearchPayersAsync(string tenantId, string? planType, string? search);
        Task<Payer> GetPayerAsync(string tenantId, string payerId);

        // Payer configuration management
        Task<List<PayerConfig>> GetPayerConfigsAsync(string tenantId, string practiceId);
        Task<PayerConfig> UpdatePayerConfigAsync(string tenantId, string practiceId, string payerId, PayerConfigUpdateRequestDto request);
    }
}