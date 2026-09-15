using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Holds the Auth0 Management API client-credentials token across requests. Registered as a
/// singleton; <see cref="Auth0ManagementProvisioner"/> itself is a transient typed client.
/// </summary>
public sealed class Auth0ManagementTokenCache
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    internal string? AccessToken { get; set; }

    internal DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// <see cref="IIdentityProvisioner"/> backed by the Auth0 Management API v2 (Spec P-2 / P-3 / P-7,
/// issue #563) — a handful of REST calls on a typed <see cref="HttpClient"/>, no SDK.
/// <para>
/// Needs only these scopes on the M2M application: <c>read:users create:users update:users
/// update:users_app_metadata read:roles create:role_members create:user_tickets</c>.
/// </para>
/// <para>
/// Never logs emails, passwords or ticket URLs. The generated password is sent once and
/// discarded; users set their own through the ticket.
/// </para>
/// </summary>
public sealed class Auth0ManagementProvisioner : IIdentityProvisioner
{
    private static readonly TimeSpan PasswordTicketLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromSeconds(60);
    private const int MaxErrorDetailLength = 300;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly Auth0ManagementTokenCache _tokenCache;
    private readonly Auth0ProvisionerOptions _options;
    private readonly ManagerAppUrlOptions _managerAppUrlOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Auth0ManagementProvisioner> _logger;

    public Auth0ManagementProvisioner(
        HttpClient httpClient,
        Auth0ManagementTokenCache tokenCache,
        IOptions<Auth0ProvisionerOptions> options,
        IOptions<ManagerAppUrlOptions> managerAppUrlOptions,
        TimeProvider timeProvider,
        ILogger<Auth0ManagementProvisioner> logger)
    {
        _httpClient = httpClient;
        _tokenCache = tokenCache;
        _options = options.Value;
        _managerAppUrlOptions = managerAppUrlOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IdentityUserResult> EnsureUserAsync(IdentityUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Role);

        // Resolve the role first, so an unknown role never leaves a user behind without one
        // (the Post-Login Action denies login to a user with no role).
        var roleId = await FindRoleIdAsync(request.Role, cancellationToken);
        var existing = await FindDatabaseUserByEmailAsync(request.Email, cancellationToken);

        var appMetadata = new
        {
            tenantId = request.TenantId,
            orgName = request.OrgName,
            locationIds = request.LocationIds,
        };

        string userId;
        bool created;

        if (existing is not null)
        {
            // A user without a matching tenantId is not ours to change: the Auth0 tenant is shared
            // with other RVS tenants and with other products.
            if (!string.Equals(existing.TenantId, request.TenantId, StringComparison.Ordinal))
            {
                throw new ConflictException("That email already belongs to a user outside this tenant.");
            }

            userId = existing.UserId ?? throw new InvalidOperationException("Auth0 returned a user without an id.");
            using var _ = await SendAsync(
                HttpMethod.Patch,
                $"api/v2/users/{Uri.EscapeDataString(userId)}",
                new { name = request.DisplayName, app_metadata = appMetadata },
                "update user",
                cancellationToken);
            created = false;
        }
        else
        {
            using var response = await SendAsync(
                HttpMethod.Post,
                "api/v2/users",
                new
                {
                    email = request.Email,
                    name = request.DisplayName,
                    connection = _options.Connection,
                    password = GeneratePassword(),
                    email_verified = false,
                    verify_email = false,
                    app_metadata = appMetadata,
                },
                "create user",
                cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<Auth0User>(JsonOptions, cancellationToken);
            userId = body?.UserId ?? throw new InvalidOperationException("Auth0 did not return an id for the created user.");
            created = true;
        }

        using (await SendAsync(
            HttpMethod.Post,
            $"api/v2/roles/{Uri.EscapeDataString(roleId)}/users",
            new { users = new[] { userId } },
            "assign role",
            cancellationToken))
        {
        }

        _logger.LogInformation(
            "Auth0 user {UserId} {Outcome} with role {Role} for tenant {TenantId}",
            userId, created ? "created" : "updated", request.Role, request.TenantId);

        return new IdentityUserResult(userId, created);
    }

    /// <inheritdoc />
    public async Task<IdentityUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using var response = await SendAsync(
            HttpMethod.Get,
            $"api/v2/users/{Uri.EscapeDataString(userId)}",
            body: null,
            "get user",
            cancellationToken,
            allowNotFound: true);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var user = await response.Content.ReadFromJsonAsync<Auth0User>(JsonOptions, cancellationToken);
        return user?.UserId is null
            ? null
            : new IdentityUser(user.UserId, user.Email ?? string.Empty, user.TenantId);
    }

    /// <inheritdoc />
    public async Task<PasswordTicket> CreatePasswordTicketAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var resultUrl = string.IsNullOrWhiteSpace(_managerAppUrlOptions.BaseUrl)
            ? null
            : _managerAppUrlOptions.BaseUrl.TrimEnd('/');

        using var response = await SendAsync(
            HttpMethod.Post,
            "api/v2/tickets/password-change",
            new
            {
                user_id = userId,
                result_url = resultUrl,
                ttl_sec = (int)PasswordTicketLifetime.TotalSeconds,
                mark_email_as_verified = true,
            },
            "create password ticket",
            cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<TicketResponse>(JsonOptions, cancellationToken);
        var url = body?.Ticket ?? throw new InvalidOperationException("Auth0 did not return a password-change ticket.");

        // The URL is a credential — log that a ticket was issued, never the ticket.
        _logger.LogInformation("Set-password ticket issued for Auth0 user {UserId}", userId);

        return new PasswordTicket(url, _timeProvider.GetUtcNow().UtcDateTime.Add(PasswordTicketLifetime));
    }

    // ---------------------------------------------------------------------------
    // Management API plumbing
    // ---------------------------------------------------------------------------

    private async Task<string> FindRoleIdAsync(string roleName, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"api/v2/roles?name_filter={Uri.EscapeDataString(roleName)}",
            body: null,
            "find role",
            cancellationToken);

        // name_filter is a substring match ("dealer:manager" also returns "dealer:regional-manager").
        var roles = await response.Content.ReadFromJsonAsync<List<Auth0Role>>(JsonOptions, cancellationToken) ?? [];
        return roles.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException(
                $"Auth0 role '{roleName}' was not found. Apply the Auth0 baseline (Infra/Auth0/auth0-apply.sh) first.");
    }

    private async Task<Auth0User?> FindDatabaseUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"api/v2/users-by-email?email={Uri.EscapeDataString(email)}",
            body: null,
            "find user by email",
            cancellationToken);

        var users = await response.Content.ReadFromJsonAsync<List<Auth0User>>(JsonOptions, cancellationToken) ?? [];

        // Only a user on our database connection counts: a social login with the same email is a
        // separate Auth0 user and does not block creating the password user.
        return users.FirstOrDefault(u => u.Identities?.Any(i =>
            string.Equals(i.Connection, _options.Connection, StringComparison.Ordinal)) == true);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string operation,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode || (allowNotFound && response.StatusCode == HttpStatusCode.NotFound))
        {
            return response;
        }

        using (response)
        {
            throw await CreateFailureAsync(response, operation, cancellationToken);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _tokenCache.Gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            var requestedAt = _timeProvider.GetUtcNow();
            using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
            {
                Content = JsonContent.Create(
                    new
                    {
                        grant_type = "client_credentials",
                        client_id = _options.ClientId,
                        client_secret = _options.ClientSecret,
                        audience = new Uri(_options.BaseUri, "api/v2/").ToString(),
                    },
                    options: JsonOptions),
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateFailureAsync(response, "token request", cancellationToken);
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new InvalidOperationException("Auth0 did not return a Management API access token.");
            }

            _tokenCache.AccessToken = token.AccessToken;
            _tokenCache.ExpiresAt = requestedAt.AddSeconds(token.ExpiresIn);
            return token.AccessToken;
        }
        finally
        {
            _tokenCache.Gate.Release();
        }
    }

    private bool TryGetCachedToken(out string token)
    {
        token = _tokenCache.AccessToken ?? string.Empty;
        return token.Length > 0 && _timeProvider.GetUtcNow() < _tokenCache.ExpiresAt - TokenRefreshMargin;
    }

    /// <summary>
    /// Builds the exception for a failed call from Auth0's error <c>message</c>. The request body
    /// (which may carry the generated password) is never included.
    /// </summary>
    private static async Task<HttpRequestException> CreateFailureAsync(
        HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var detail = string.Empty;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                detail = error.Message.Length > MaxErrorDetailLength
                    ? ": " + error.Message[..MaxErrorDetailLength]
                    : ": " + error.Message;
            }
        }
        catch (JsonException)
        {
            // Not a JSON error body; the status code alone will do.
        }

        return new HttpRequestException(
            $"Auth0 Management API {operation} failed ({(int)response.StatusCode}){detail}",
            inner: null,
            statusCode: response.StatusCode);
    }

    /// <summary>A random password the user never sees; they set their own through the ticket.</summary>
    private static string GeneratePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_')
        // Guarantees every character class for Auth0's password-strength policy.
        + "aA1!";

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record TicketResponse(
        [property: JsonPropertyName("ticket")] string? Ticket);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("message")] string? Message);

    private sealed record Auth0Role(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record Auth0Identity(
        [property: JsonPropertyName("connection")] string? Connection);

    private sealed record Auth0User(
        [property: JsonPropertyName("user_id")] string? UserId,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("identities")] List<Auth0Identity>? Identities,
        [property: JsonPropertyName("app_metadata")] Dictionary<string, JsonElement>? AppMetadata)
    {
        public string? TenantId =>
            AppMetadata is not null
            && AppMetadata.TryGetValue("tenantId", out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
