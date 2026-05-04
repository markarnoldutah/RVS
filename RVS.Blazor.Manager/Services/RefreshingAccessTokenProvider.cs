using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;

namespace RVS.Blazor.Manager.Services;

/// <summary>
/// Wraps the default <see cref="IAccessTokenProvider"/> from
/// <c>AddOidcAuthentication</c> with a refresh-token fallback that talks directly
/// to Auth0's <c>/oauth/token</c> endpoint. Required so users stay signed in for
/// the full 15-day rolling refresh-token lifetime (RVS_Technical_PRD.md §10.1)
/// instead of being bounced to login when the default iframe silent-renewal path
/// fails under third-party-cookie blocking (Safari ITP, Chrome 3rd-party-cookie
/// phase-out) or after the Auth0 SSO cookie idles out (default 3 days).
///
/// Why: without this wrapper the long-lived refresh_token sits in sessionStorage
/// unused — the Microsoft library never POSTs it to the token endpoint and instead
/// relies on a hidden-iframe SSO check that browsers increasingly block.
/// </summary>
public sealed class RefreshingAccessTokenProvider : IAccessTokenProvider
{
    private const int RefreshSkewSeconds = 60;

    private readonly IAccessTokenProvider _inner;
    private readonly HttpClient _tokenEndpointClient;
    private readonly IJSRuntime _js;
    private readonly string _authority;
    private readonly string _clientId;
    private readonly ILogger<RefreshingAccessTokenProvider> _log;

    public RefreshingAccessTokenProvider(
        IAccessTokenProvider inner,
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        IConfiguration config,
        ILogger<RefreshingAccessTokenProvider> log)
    {
        _inner = inner;
        _js = js;
        _log = log;
        _tokenEndpointClient = httpClientFactory.CreateClient("Auth0.Token");
        _authority = config["Auth0:Authority"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Auth0:Authority is not configured.");
        _clientId = config["Auth0:ClientId"]
            ?? throw new InvalidOperationException("Auth0:ClientId is not configured.");
    }

    public async ValueTask<AccessTokenResult> RequestAccessToken()
    {
        var result = await _inner.RequestAccessToken();
        if (await TryReturnFreshAsync(result) is { } fresh)
        {
            return fresh;
        }
        return await TryRefreshAndRetryAsync(result);
    }

    public async ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
    {
        var result = await _inner.RequestAccessToken(options);
        if (await TryReturnFreshAsync(result) is { } fresh)
        {
            return fresh;
        }
        return await TryRefreshAndRetryAsync(result);
    }

    private async ValueTask<AccessTokenResult?> TryReturnFreshAsync(AccessTokenResult result)
    {
        if (result.TryGetToken(out var token) && token is not null && !await IsExpiringSoonAsync())
        {
            return result;
        }
        return null;
    }

    private async ValueTask<AccessTokenResult> TryRefreshAndRetryAsync(AccessTokenResult original)
    {
        try
        {
            var stored = await _js.InvokeAsync<StoredToken?>("rvsAuth_getStoredToken", _clientId);
            if (stored is null || string.IsNullOrEmpty(stored.RefreshToken))
            {
                return original;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_authority}/oauth/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = _clientId,
                    ["refresh_token"] = stored.RefreshToken,
                }),
            };

            using var response = await _tokenEndpointClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Auth0 refresh-token exchange returned {Status}.", (int)response.StatusCode);
                return original;
            }

            var payload = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            {
                return original;
            }

            await _js.InvokeAsync<bool>("rvsAuth_applyRefreshedToken", _clientId, payload);

            return await _inner.RequestAccessToken();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to refresh access token via refresh_token grant.");
            return original;
        }
    }

    private async ValueTask<bool> IsExpiringSoonAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<StoredToken?>("rvsAuth_getStoredToken", _clientId);
            if (stored is null || stored.ExpiresAt <= 0)
            {
                return false;
            }
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return stored.ExpiresAt - nowUnix <= RefreshSkewSeconds;
        }
        catch
        {
            return false;
        }
    }

    private sealed record StoredToken(
        [property: JsonPropertyName("accessToken")] string? AccessToken,
        [property: JsonPropertyName("expiresAt")] long ExpiresAt,
        [property: JsonPropertyName("refreshToken")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record RefreshTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}
