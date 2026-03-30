using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="TenantConfig"/> entities via the canonical
/// <see cref="ITenantConfigService"/> interface, backed by <see cref="ITenantConfigRepository"/>.
/// </summary>
public sealed class TenantConfigService : ITenantConfigService
{
    private readonly ITenantConfigRepository _repository;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantConfigService"/>.
    /// </summary>
    public TenantConfigService(ITenantConfigRepository repository, IUserContextAccessor userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<TenantConfig> CreateTenantConfigAsync(
        string tenantId,
        TenantConfigCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _repository.GetAsync(tenantId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Tenant config already exists for tenant '{tenantId}'.");

        var entity = request.ToEntity(tenantId, _userContext.UserId);
        return await _repository.CreateAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TenantConfig> GetTenantConfigAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return await _repository.GetAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant config not found for tenant '{tenantId}'.");
    }

    /// <inheritdoc />
    public async Task<TenantConfig> UpdateTenantConfigAsync(
        string tenantId,
        TenantConfigUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _repository.GetAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant config not found for tenant '{tenantId}'.");

        entity.ApplyUpdateFromDto(request);
        entity.MarkAsUpdated(_userContext.UserId);
        await _repository.SaveAsync(entity, cancellationToken);

        return entity;
    }

    /// <inheritdoc />
    public async Task<TenantAccessGateEmbedded> GetAccessGateAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var config = await _repository.GetAsync(tenantId, cancellationToken);
        return config?.AccessGate ?? new TenantAccessGateEmbedded { LoginsEnabled = true };
    }
}
