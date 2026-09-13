namespace RVS.UI.Shared.Services;

/// <summary>
/// Revokes a refresh token at Auth0's <c>/oauth/revoke</c> endpoint when a manager signs out
/// (issue #498). The manager app can keep a sign-in across restarts, so deleting the local copy
/// is not enough: a token copied earlier would otherwise stay valid until it expired.
///
/// The manager app is a public client (<c>token_endpoint_auth_method: none</c>), so the request
/// carries only <c>client_id</c> and <c>token</c>. Revocation is best effort and never throws on
/// a rejected or unreachable request — sign-out must always proceed.
/// </summary>
public sealed class RefreshTokenRevocationClient
{
    private readonly HttpClient _httpClient;
    private readonly string _revokeUrl;
    private readonly string _clientId;

    /// <summary>
    /// Initializes a new instance of <see cref="RefreshTokenRevocationClient"/>.
    /// </summary>
    /// <param name="httpClient">A client that does not attach the API bearer token.</param>
    /// <param name="authority">The Auth0 authority, e.g. <c>https://tenant.us.auth0.com/</c>.</param>
    /// <param name="clientId">The manager app's Auth0 client id.</param>
    public RefreshTokenRevocationClient(HttpClient httpClient, string authority, string clientId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        _httpClient = httpClient;
        _revokeUrl = $"{authority.Trim().TrimEnd('/')}/oauth/revoke";
        _clientId = clientId;
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

        try
        {
            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("token", refreshToken),
            ]);

            using var response = await _httpClient.PostAsync(_revokeUrl, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout — treat like an unreachable endpoint.
            return false;
        }
    }
}
