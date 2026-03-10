using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces
{
    public interface IPracticeService
    {
        Task<List<Practice>> GetPracticesAsync(string tenantId, bool includeLocations);
        Task<Practice> GetPracticeAsync(string tenantId, string practiceId);
    }
}