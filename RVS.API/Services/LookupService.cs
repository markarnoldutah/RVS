using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RVS.API.Services
{
    public sealed class LookupService : ILookupService
    {
        private readonly ILookupRepository _repository;

        public LookupService(ILookupRepository repository)
        {
            _repository = repository;
        }

        public async Task<LookupSetDto> GetLookupSetAsync(string tenantId, string lookupSetId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lookupSetId);

            // MVP: only global lookups are supported.
            var global = await _repository.GetGlobalAsync(lookupSetId, cancellationToken)
                ?? throw new KeyNotFoundException($"LookupSetId '{lookupSetId}' was not found.");

            // In the future we will:
            //  - query for tenant-specific overrides
            //  - merge according to OverrideMode
            // For now, we simply return the global set as-is.
            return global.ToDto();
        }

        // Optional: helper for strongly-typed categories (enum-based) if you want:
        // public Task<LookupSetDto> GetLookupSetAsync(string tenantId, LookupCategories category) =>
        //     GetLookupSetAsync(tenantId, category.ToCategoryString());
    }
}
