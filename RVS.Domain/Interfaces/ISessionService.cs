using System.Security.Claims;
using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces
{
    public interface ISessionService
    {
        Task<UserSessionContextDto> GetContextAsync(ClaimsPrincipal user);
    }
}