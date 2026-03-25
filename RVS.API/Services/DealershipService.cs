using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="Dealership"/> entities.
/// </summary>
public sealed class DealershipService : IDealershipService
{
    private readonly IDealershipRepository _repository;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="DealershipService"/>.
    /// </summary>
    public DealershipService(IDealershipRepository repository, IUserContextAccessor userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dealership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return await _repository.ListByTenantAsync(tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dealership> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Dealership '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<Dealership> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return await _repository.GetBySlugAsync(slug, cancellationToken)
            ?? throw new KeyNotFoundException($"Dealership with slug '{slug}' not found.");
    }

    /// <inheritdoc />
    public async Task<Dealership> CreateAsync(string tenantId, Dealership entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entity);

        return await _repository.CreateAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dealership> UpdateAsync(string tenantId, string id, Dealership entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entity);

        var existing = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Dealership '{id}' not found.");

        existing.Name = entity.Name;
        existing.Slug = entity.Slug;
        existing.LogoUrl = entity.LogoUrl;
        existing.ServiceEmail = entity.ServiceEmail;
        existing.Phone = entity.Phone;
        existing.IntakeConfig = entity.IntakeConfig;
        existing.MarkAsUpdated(_userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
