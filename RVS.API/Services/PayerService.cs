using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services
{
    public class PayerService : IPayerService
    {
        private readonly IPayerRepository _payerRepository;
        private readonly IConfigRepository _configRepository;
        private readonly IPracticeRepository _practiceRepository;
        private readonly IUserContextAccessor _userContext;

        public PayerService(
            IPayerRepository payerRepository,
            IConfigRepository configRepository,
            IPracticeRepository practiceRepository,
            IUserContextAccessor userContext)
        {
            _payerRepository = payerRepository;
            _configRepository = configRepository;
            _practiceRepository = practiceRepository;
            _userContext = userContext;
        }

        // -------------------------
        // Payer lookup and search
        // -------------------------
        public async Task<List<Payer>> SearchPayersAsync(string tenantId, string? planType, string? search)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            
            var payers = await _payerRepository.SearchAsync(tenantId, planType, search);
            
            return payers;
        }

        public async Task<Payer> GetPayerAsync(string tenantId, string payerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(payerId);

            var payer = await _payerRepository.GetByIdAsync(tenantId, payerId);
            if (payer is null)
                throw new KeyNotFoundException("Payer not found.");

            return payer;
        }

        // -------------------------
        // Payer configuration management
        // -------------------------
        public async Task<List<PayerConfig>> GetPayerConfigsAsync(string tenantId, string practiceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);

            // Validate practice exists (helps catch client bugs early)
            var practice = await _practiceRepository.GetByIdAsync(tenantId, practiceId);
            if (practice is null)
                throw new KeyNotFoundException("Practice not found.");

            return await _configRepository.GetPayerConfigsAsync(tenantId, practiceId);
        }

        public async Task<PayerConfig> UpdatePayerConfigAsync(string tenantId, string practiceId, string payerId, PayerConfigUpdateRequestDto request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(payerId);
            ArgumentNullException.ThrowIfNull(request);

            var payerCfg = await _configRepository.GetPayerConfigAsync(tenantId, practiceId, payerId);
            if (payerCfg is null)
                throw new KeyNotFoundException("Payer config not found.");

            // Minimal, safe updates supported by existing DTO (expand later)
            payerCfg.IsEnabled = request.IsEnabled ?? payerCfg.IsEnabled;
            payerCfg.DisplayName = request.DisplayNameOverride ?? payerCfg.DisplayName;
            payerCfg.MarkAsUpdated(_userContext.UserId);

            await _configRepository.SavePayerConfigAsync(tenantId, payerCfg);

            return payerCfg;
        }
    }
}
