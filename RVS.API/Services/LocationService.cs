using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

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
    private readonly ILogger<LocationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LocationService"/>.
    /// </summary>
    public LocationService(
        ILocationRepository locationRepository,
        ISlugLookupRepository slugLookupRepository,
        IDealershipRepository dealershipRepository,
        IUserContextAccessor userContext,
        ILogger<LocationService> logger)
    {
        _locationRepository = locationRepository;
        _slugLookupRepository = slugLookupRepository;
        _dealershipRepository = dealershipRepository;
        _userContext = userContext;
        _logger = logger;
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

        // Auto-generate a unique slug from the dealership ("org") slug + location name when
        // the caller did not supply one. This keeps slugs uniform, human-readable, and unique
        // per tenant without requiring the UI to pick a slug.
        if (string.IsNullOrWhiteSpace(entity.Slug))
        {
            entity.Slug = await GenerateUniqueSlugAsync(tenantId, entity.Name, cancellationToken);
        }
        else
        {
            // Caller supplied a slug — make sure it is not already taken.
            var existing = await _slugLookupRepository.GetBySlugAsync(entity.Slug, cancellationToken);
            if (existing is not null)
            {
                throw new ArgumentException($"Slug '{entity.Slug}' is already in use.", nameof(entity));
            }
        }

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to rollback slug '{Slug}' after location creation failure", entity.Slug);
            }

            throw;
        }
    }

    /// <summary>
    /// Builds a slug shaped like <c>{dealership-slug}-{location-name}</c> and probes the slug-lookup
    /// store for collisions, appending <c>-2</c>, <c>-3</c>, … until a free slug is found.
    /// Falls back to just the location-name slug when the tenant has no dealership yet.
    /// </summary>
    private async Task<string> GenerateUniqueSlugAsync(string tenantId, string locationName, CancellationToken cancellationToken)
    {
        var dealerships = await _dealershipRepository.ListByTenantAsync(tenantId, cancellationToken);
        var orgSlug = dealerships.FirstOrDefault()?.Slug;

        var baseSlug = SlugGenerator.ForLocation(orgSlug, locationName);
        if (string.IsNullOrEmpty(baseSlug))
        {
            // Defensive fallback: a location-id-derived slug is always non-empty and unique.
            baseSlug = $"location-{Guid.NewGuid():N}"[..32];
        }

        // Cap the base so suffix variants stay within MaxSlugLength.
        const int suffixReserve = 4; // supports up to "-9999"
        if (baseSlug.Length > SlugGenerator.MaxSlugLength - suffixReserve)
        {
            baseSlug = baseSlug[..(SlugGenerator.MaxSlugLength - suffixReserve)].TrimEnd('-');
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await _slugLookupRepository.GetBySlugAsync(candidate, cancellationToken) is not null)
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old slug '{Slug}' during location update", oldSlug);
            }
        }

        existing.Name = entity.Name;
        existing.Slug = entity.Slug;
        existing.Phone = entity.Phone;
        existing.Address = entity.Address;
        existing.IntakeConfig = entity.IntakeConfig;
        existing.EnabledCapabilities = entity.EnabledCapabilities;
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete slug '{Slug}' during location delete", existing.Slug);
        }
    }
}
