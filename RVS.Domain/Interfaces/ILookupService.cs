using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces
{
    public interface ILookupService
    {
        /// <summary>
        /// Gets the lookup set visible to the given tenant for the specified category.
        /// For MVP, this always returns the global set only.
        /// </summary>
        Task<LookupSetDto> GetLookupSetAsync(string tenantId, string category);
    }
}