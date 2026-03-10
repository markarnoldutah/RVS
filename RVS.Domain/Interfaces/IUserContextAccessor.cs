namespace RVS.Domain.Interfaces;

/// <summary>
/// Provides access to the current authenticated user's identity for audit tracking.
/// </summary>
public interface IUserContextAccessor
{
    /// <summary>
    /// Gets the current user's ID, or null if not authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current user's tenant ID, or null if not available.
    /// </summary>
    string? TenantId { get; }
}