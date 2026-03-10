using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// Centralized cache for all lookup data. Loads once after login and provides
/// synchronous access to cached lookups throughout the application.
/// </summary>
public interface ILookupCacheService
{
    /// <summary>
    /// Indicates whether the cache has been loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Loads all lookups from the API. Should be called once after authentication.
    /// </summary>
    Task LoadAsync(string practiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cached data (e.g., on logout or practice change).
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cached visit types. Returns empty list if not loaded.
    /// </summary>
    IReadOnlyList<LookupItemDto> VisitTypes { get; }

    /// <summary>
    /// Gets cached plan types. Returns empty list if not loaded.
    /// </summary>
    IReadOnlyList<LookupItemDto> PlanTypes { get; }

    /// <summary>
    /// Gets cached relationship types. Returns empty list if not loaded.
    /// </summary>
    IReadOnlyList<LookupItemDto> RelationshipTypes { get; }

    /// <summary>
    /// Gets cached COB reasons. Returns empty list if not loaded.
    /// </summary>
    IReadOnlyList<LookupItemDto> CobReasons { get; }

    /// <summary>
    /// Gets cached payers. Returns empty list if not loaded.
    /// </summary>
    IReadOnlyList<PayerResponseDto> Payers { get; }

    /// <summary>
    /// Gets cached locations for the current practice. Returns empty list if not loaded.
    /// </summary>
    IReadOnlyList<LocationSummaryResponseDto> Locations { get; }

    /// <summary>
    /// Event raised when the cache has finished loading.
    /// </summary>
    event EventHandler? CacheLoaded;
}

public class LookupCacheService : ILookupCacheService
{
    private readonly ILookupApiService _lookupApiService;
    private readonly ILogger<LookupCacheService> _logger;

    private List<LookupItemDto> _visitTypes = [];
    private List<LookupItemDto> _planTypes = [];
    private List<LookupItemDto> _relationshipTypes = [];
    private List<LookupItemDto> _cobReasons = [];
    private List<PayerResponseDto> _payers = [];
    private List<LocationSummaryResponseDto> _locations = [];

    private string? _loadedPracticeId;
    private bool _isLoading;

    public LookupCacheService(
        ILookupApiService lookupApiService,
        ILogger<LookupCacheService> logger)
    {
        _lookupApiService = lookupApiService;
        _logger = logger;
    }

    public bool IsLoaded => _loadedPracticeId is not null;

    public IReadOnlyList<LookupItemDto> VisitTypes => _visitTypes;
    public IReadOnlyList<LookupItemDto> PlanTypes => _planTypes;
    public IReadOnlyList<LookupItemDto> RelationshipTypes => _relationshipTypes;
    public IReadOnlyList<LookupItemDto> CobReasons => _cobReasons;
    public IReadOnlyList<PayerResponseDto> Payers => _payers;
    public IReadOnlyList<LocationSummaryResponseDto> Locations => _locations;

    public event EventHandler? CacheLoaded;

    public async Task LoadAsync(string practiceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);

        // Skip if already loaded for this practice or currently loading
        if (_loadedPracticeId == practiceId || _isLoading)
        {
            _logger.LogDebug("Lookup cache already loaded or loading for practice {PracticeId}", practiceId);
            return;
        }

        _isLoading = true;

        try
        {
            _logger.LogInformation("Loading lookup cache for practice {PracticeId}", practiceId);

            // Load all lookups in parallel for efficiency
            var visitTypesTask = _lookupApiService.GetVisitTypesAsync(cancellationToken);
            var planTypesTask = _lookupApiService.GetPlanTypesAsync(cancellationToken);
            var relationshipTypesTask = _lookupApiService.GetRelationshipTypesAsync(cancellationToken);
            var cobReasonsTask = _lookupApiService.GetCobReasonsAsync(cancellationToken);
            var payersTask = _lookupApiService.GetPayersAsync(cancellationToken);
            var locationsTask = _lookupApiService.GetLocationsAsync(practiceId, cancellationToken);

            await Task.WhenAll(
                visitTypesTask,
                planTypesTask,
                relationshipTypesTask,
                cobReasonsTask,
                payersTask,
                locationsTask);

            _visitTypes = await visitTypesTask;
            _planTypes = await planTypesTask;
            _relationshipTypes = await relationshipTypesTask;
            _cobReasons = await cobReasonsTask;
            _payers = await payersTask;
            _locations = await locationsTask;

            _loadedPracticeId = practiceId;

            _logger.LogInformation(
                "Lookup cache loaded: {VisitTypes} visit types, {PlanTypes} plan types, " +
                "{RelationshipTypes} relationship types, {CobReasons} COB reasons, " +
                "{Payers} payers, {Locations} locations",
                _visitTypes.Count, _planTypes.Count, _relationshipTypes.Count,
                _cobReasons.Count, _payers.Count, _locations.Count);

            CacheLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load lookup cache for practice {PracticeId}", practiceId);
            throw;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void Clear()
    {
        _visitTypes = [];
        _planTypes = [];
        _relationshipTypes = [];
        _cobReasons = [];
        _payers = [];
        _locations = [];
        _loadedPracticeId = null;

        _logger.LogInformation("Lookup cache cleared");
    }
}
