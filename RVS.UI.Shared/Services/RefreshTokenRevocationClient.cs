namespace RVS.UI.Shared.Services;

/// <summary>
/// Revokes a refresh token at Auth0's <c>/oauth/revoke</c> endpoint when a manager signs out
/// (issue #498). The manager app can keep a sign-in across restarts, so deleting the local copy
/// is not enough: a token copied earlier would otherwise stay valid until it expired.
///
/// The manager app is a public client (<c>token_endpoint_auth_method: none</c>), so the request
/// carries only <c>client_id</c> and <c>token</c>. Revocation is best effort and never throws on
/// a rejected, unreachable or unanswered request — sign-out must always proceed, so every
/// attempt is bounded by a timeout (issue #625).
/// </summary>
public sealed class RefreshTokenRevocationClient
{
    private readonly HttpClient _httpClient;
    private readonly string _revokeUrl;
    private readonly string _clientId;
    private readonly TimeSpan _timeout;

    /// <summary>How long a revoke may take before sign-out goes ahead without it.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of <see cref="RefreshTokenRevocationClient"/>.
    /// </summary>
    /// <param name="httpClient">A client that does not attach the API bearer token.</param>
    /// <param name="authority">The Auth0 authority, e.g. <c>https://tenant.us.auth0.com/</c>.</param>
    /// <param name="clientId">The manager app's Auth0 client id.</param>
    /// <param name="timeout">Upper bound on one revoke; defaults to <see cref="DefaultTimeout"/>.</param>
    public RefreshTokenRevocationClient(HttpClient httpClient, string authority, string clientId, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout ?? DefaultTimeout, TimeSpan.Zero, nameof(timeout));

        _httpClient = httpClient;
        _revokeUrl = $"{authority.Trim().TrimEnd('/')}/oauth/revoke";
        _clientId = clientId;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>
    /// Revokes <paramref name="refreshToken"/>.
    /// </summary>
    /// <returns><c>true</c> when Auth0 accepted the revocation; <c>false</c> when there was no token or the request failed.</returns>
    public async Task<bool> RevokeAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("token", refreshToken),
            ]);

            using var response = await _httpClient.PostAsync(_revokeUrl, content, timeoutCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our timeout or HttpClient's — treat like an unreachable endpoint.
            return false;
        }
    }
}
