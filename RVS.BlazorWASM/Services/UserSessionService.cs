using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Security.Claims;
using System.Text.Json;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// Manages the user session lifecycle including authentication state changes
/// and tenant context.
/// </summary>
public interface IUserSessionService : IDisposable
{
    /// <summary>
    /// Indicates whether the user session has been fully initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// The current user's tenant ID, or null if not authenticated.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Ensures the session is initialized. Components can call this if they
    /// need to guarantee initialization before proceeding.
    /// </summary>
    Task EnsureInitializedAsync();

    /// <summary>
    /// Raised when the session has been initialized after login.
    /// </summary>
    event EventHandler? SessionInitialized;

    /// <summary>
    /// Raised when the session has been cleared (logout).
    /// </summary>
    event EventHandler? SessionCleared;
}

/// <summary>
/// Implementation of <see cref="IUserSessionService"/> that subscribes to
/// authentication state changes and manages session initialization.
/// </summary>
public sealed class UserSessionService : IUserSessionService
{
    // Claims in the access token (from Auth0)
    private const string TenantIdClaimType = "http://benefetch.com/tenantId";

    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILookupCacheService _lookupCache;
    private readonly ILogger<UserSessionService> _logger;

    private string? _tenantId;
    private bool _isInitialized;
    private bool _isInitializing;
    private bool _isDisposed;

    public UserSessionService(
        AuthenticationStateProvider authStateProvider,
        IAccessTokenProvider accessTokenProvider,
        ILookupCacheService lookupCache,
        ILogger<UserSessionService> logger)
    {
        _authStateProvider = authStateProvider;
        _accessTokenProvider = accessTokenProvider;
        _lookupCache = lookupCache;
        _logger = logger;

        // Subscribe to authentication state changes
        _authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    public bool IsInitialized => _isInitialized;
    public string? TenantId => _tenantId;

    public event EventHandler? SessionInitialized;
    public event EventHandler? SessionCleared;

    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized || _isInitializing)
            return;

        try
        {
            _isInitializing = true;
            var authState = await _authStateProvider.GetAuthenticationStateAsync();

            if (authState.User.Identity?.IsAuthenticated == true)
            {
                await InitializeSessionAsync();
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        if (_isDisposed) return;

        try
        {
            var authState = await task;

            if (authState.User.Identity?.IsAuthenticated == true)
            {
                await InitializeSessionAsync();
            }
            else
            {
                ClearSession();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle authentication state change");
        }
    }

    private async Task InitializeSessionAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            _isInitializing = true;

            // Extract claims from the access token (not ID token)
            // Tenant claims are typically in the access token from Auth0
            await ExtractAccessTokenClaimsAsync();

            if (string.IsNullOrWhiteSpace(_tenantId))
            {
                _logger.LogWarning(
                    "User authenticated but no tenant ID found in access token. " +
                    "Expected claim type: {ClaimType}. Ensure Auth0 is configured to include this claim.",
                    TenantIdClaimType);
                return;
            }

            _isInitialized = true;
            _logger.LogInformation(
                "User session initialized. TenantId={TenantId}",
                _tenantId);

            SessionInitialized?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize user session");
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task ExtractAccessTokenClaimsAsync()
    {
        try
        {
            var tokenResult = await _accessTokenProvider.RequestAccessToken();

            if (!tokenResult.TryGetToken(out var accessToken))
            {
                _logger.LogWarning("Failed to obtain access token for claims extraction");
                return;
            }

            // Parse the JWT payload (access tokens are JWTs)
            var claims = ParseJwtClaims(accessToken.Value);

            // Log all claims for debugging (only in development)
            _logger.LogDebug("Access token contains {ClaimCount} claims", claims.Count);
            foreach (var claim in claims)
            {
                _logger.LogDebug("  Claim: {ClaimType} = {ClaimValue}", claim.Key, claim.Value);
            }

            // Extract tenant ID
            if (claims.TryGetValue(TenantIdClaimType, out var tenantIdValue))
            {
                _tenantId = GetStringValue(tenantIdValue);
                _logger.LogDebug("Found tenant ID: {TenantId}", _tenantId);
            }
            else
            {
                _logger.LogWarning("Tenant ID claim not found. Expected: {ClaimType}", TenantIdClaimType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract claims from access token");
        }
    }

    private static Dictionary<string, object> ParseJwtClaims(string jwt)
    {
        // JWT format: header.payload.signature
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return [];

        var payload = parts[1];

        // Base64Url decode
        var base64Payload = payload
            .Replace('-', '+')
            .Replace('_', '/')
            .PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        var jsonBytes = Convert.FromBase64String(base64Payload);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes) ?? [];
    }

    private static string? GetStringValue(object? value)
    {
        if (value is null)
            return null;

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
            return jsonElement.GetString();

        if (value is string str)
            return str;

        return value.ToString();
    }

    private void ClearSession()
    {
        _tenantId = null;
        _isInitialized = false;

        _lookupCache.Clear();

        _logger.LogInformation("User session cleared");
        SessionCleared?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
