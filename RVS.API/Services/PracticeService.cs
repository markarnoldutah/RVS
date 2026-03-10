using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services
{
    public class PracticeService : IPracticeService
    {
        private readonly IPracticeRepository _practiceRepository;

        public PracticeService(IPracticeRepository practiceRepository)
        {
            _practiceRepository = practiceRepository;
        }

        public async Task<List<Practice>> GetPracticesAsync(string tenantId, bool includeLocations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            var practices = await _practiceRepository.GetPracticesForTenantAsync(tenantId, includeLocations);
            
            return practices;
        }

        public async Task<Practice> GetPracticeAsync(string tenantId, string practiceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);

            var entity = await _practiceRepository.GetByIdAsync(tenantId, practiceId);
            if (entity is null)
                throw new KeyNotFoundException("Practice not found.");

            return entity;
        }
    }
}
