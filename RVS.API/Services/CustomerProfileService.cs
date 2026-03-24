using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="CustomerProfile"/> entities.
/// Tenant-scoped — resolves by email within a tenant, supports asset ownership tracking
/// with three-branch transfer logic.
/// </summary>
public sealed class CustomerProfileService : ICustomerProfileService
{
    private readonly ICustomerProfileRepository _repository;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="CustomerProfileService"/>.
    /// </summary>
    public CustomerProfileService(ICustomerProfileRepository repository, IUserContextAccessor userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<CustomerProfile> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer profile '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<CustomerProfile> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return await _repository.GetByEmailAsync(tenantId, email, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer profile for email '{email}' not found in tenant '{tenantId}'.");
    }

    /// <inheritdoc />
    public async Task<CustomerProfile> GetOrCreateAsync(string tenantId, string email, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await _repository.GetByEmailAsync(tenantId, normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var profile = new CustomerProfile
        {
            TenantId = tenantId,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Name = $"{firstName.Trim()} {lastName.Trim()}",
            CreatedByUserId = _userContext.UserId,
        };

        return await _repository.CreateAsync(profile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CustomerProfile> UpdateAsync(string tenantId, string id, CustomerProfile entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entity);

        var existing = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer profile '{id}' not found.");

        existing.FirstName = entity.FirstName;
        existing.LastName = entity.LastName;
        existing.Phone = entity.Phone;
        existing.Name = $"{entity.FirstName} {entity.LastName}";
        existing.MarkAsUpdated(_userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CustomerProfile> ResolveAndTrackAssetAsync(string tenantId, string email, string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var profile = await _repository.GetByEmailAsync(tenantId, normalizedEmail, cancellationToken)
            ?? await CreateProfileAsync(tenantId, email, cancellationToken);

        var existingOwner = await _repository.GetByActiveAssetIdAsync(tenantId, assetId, cancellationToken);

        // Branch 1: Asset is active under a DIFFERENT profile in the same tenant → transfer ownership.
        if (existingOwner is not null && existingOwner.Id != profile.Id)
        {
            existingOwner.DeactivateAsset(assetId);
            existingOwner.MarkAsUpdated(_userContext.UserId);
            await _repository.UpdateAsync(existingOwner, cancellationToken);
        }

        // Branch 2 & 3: Activate new asset or refresh existing one on the current profile.
        profile.ActivateOrRefreshAsset(assetId);
        profile.MarkAsUpdated(_userContext.UserId);
        return await _repository.UpdateAsync(profile, cancellationToken);
    }

    private async Task<CustomerProfile> CreateProfileAsync(string tenantId, string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var profile = new CustomerProfile
        {
            TenantId = tenantId,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            Name = string.Empty,
            CreatedByUserId = _userContext.UserId,
        };

        return await _repository.CreateAsync(profile, cancellationToken);
    }
}
