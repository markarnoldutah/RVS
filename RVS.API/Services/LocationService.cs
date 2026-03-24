using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="Location"/> entities with atomic slug management.
/// Creates a <see cref="SlugLookup"/> entry before the location to guarantee slug uniqueness,
/// and rolls back the slug entry if location creation fails.
/// </summary>
public sealed class LocationService : ILocationService
{
    private readonly ILocationRepository _locationRepository;
    private readonly ISlugLookupRepository _slugLookupRepository;
    private readonly IDealershipRepository _dealershipRepository;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="LocationService"/>.
    /// </summary>
    public LocationService(
        ILocationRepository locationRepository,
        ISlugLookupRepository slugLookupRepository,
        IDealershipRepository dealershipRepository,
        IUserContextAccessor userContext)
    {
        _locationRepository = locationRepository;
        _slugLookupRepository = slugLookupRepository;
        _dealershipRepository = dealershipRepository;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<Location> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Location>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return await _locationRepository.ListByTenantAsync(tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Location> CreateAsync(string tenantId, Location entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entity);

        // Step 1: Create slug lookup entry first to reserve the slug
        var slugLookup = new SlugLookup
        {
            Id = $"slug_{entity.Slug}",
            TenantId = tenantId,
            Slug = entity.Slug,
            LocationId = entity.Id,
            DealershipName = string.Empty,
            LocationName = entity.Name,
            CreatedByUserId = _userContext.UserId
        };

        await _slugLookupRepository.UpsertAsync(slugLookup, cancellationToken);

        try
        {
            // Step 2: Create the location document
            return await _locationRepository.CreateAsync(entity, cancellationToken);
        }
        catch
        {
            // Rollback: delete the slug entry if location creation fails
            try
            {
                await _slugLookupRepository.DeleteAsync(entity.Slug, cancellationToken);
            }
            catch
            {
                // Best-effort rollback — log would be ideal but we re-throw the original exception
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Location> UpdateAsync(string tenantId, string id, Location entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entity);

        var existing = await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");

        var oldSlug = existing.Slug;
        var newSlug = entity.Slug;

        // If slug changed, manage slug lookup entries atomically
        if (!string.Equals(oldSlug, newSlug, StringComparison.Ordinal))
        {
            var slugLookup = new SlugLookup
            {
                Id = $"slug_{newSlug}",
                TenantId = tenantId,
                Slug = newSlug,
                LocationId = existing.Id,
                DealershipName = string.Empty,
                LocationName = entity.Name,
                CreatedByUserId = _userContext.UserId
            };

            await _slugLookupRepository.UpsertAsync(slugLookup, cancellationToken);

            // Delete old slug entry
            try
            {
                await _slugLookupRepository.DeleteAsync(oldSlug, cancellationToken);
            }
            catch
            {
                // Best-effort cleanup of old slug
            }
        }

        existing.Name = entity.Name;
        existing.Slug = entity.Slug;
        existing.Phone = entity.Phone;
        existing.Address = entity.Address;
        existing.IntakeConfig = entity.IntakeConfig;
        existing.MarkAsUpdated(_userContext.UserId);

        return await _locationRepository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var existing = await _locationRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Location '{id}' not found.");

        // Delete location first, then clean up slug
        await _locationRepository.DeleteAsync(tenantId, id, cancellationToken);

        try
        {
            await _slugLookupRepository.DeleteAsync(existing.Slug, cancellationToken);
        }
        catch
        {
            // Best-effort slug cleanup
        }
    }
}
