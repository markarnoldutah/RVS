using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RVS.Domain.DTOs;

namespace RVS.Blazor.Manager.Services;

/// <summary>
/// Typed client for the platform-admin provisioning endpoints, <c>api/admin/tenants</c>
/// (issue #563). Lives in the Manager app rather than <c>RVS.UI.Shared</c>: nothing else may call
/// these endpoints. The API is the only gate — a non-admin gets <see cref="AdminApiException.IsForbidden"/>.
/// </summary>
public sealed class AdminApiClient
{
    private const string BasePath = "api/admin/tenants";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The bearer-token <see cref="HttpClient"/> for the RVS API.</param>
    public AdminApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>Lists every tenant.</summary>
    public async Task<List<TenantSummaryResponseDto>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(BasePath, cancellationToken);
        return await ReadAsync<List<TenantSummaryResponseDto>>(response, cancellationToken);
    }

    /// <summary>Edits a tenant's status, plan, billing email or notes.</summary>
    public async Task<TenantSummaryResponseDto> UpdateTenantAsync(
        string tenantId, TenantUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(TenantPath(tenantId), request, cancellationToken);
        return await ReadAsync<TenantSummaryResponseDto>(response, cancellationToken);
    }

    /// <summary>Provisions a tenant. Re-submitting the same request finishes a partial run.</summary>
    public async Task<TenantProvisioningResponseDto> CreateTenantAsync(
        TenantCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(BasePath, request, cancellationToken);
        return await ReadAsync<TenantProvisioningResponseDto>(response, cancellationToken);
    }

    /// <summary>Adds a user to a tenant and returns a set-password link.</summary>
    public async Task<TenantUserProvisioningResponseDto> AddUserAsync(
        string tenantId, TenantUserCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"{TenantPath(tenantId)}/users", request, cancellationToken);
        return await ReadAsync<TenantUserProvisioningResponseDto>(response, cancellationToken);
    }

    /// <summary>Issues a new set-password link for an existing user.</summary>
    public async Task<PasswordTicketResponseDto> CreatePasswordTicketAsync(
        string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using var response = await _httpClient.PostAsync(
            $"{TenantPath(tenantId)}/users/{Uri.EscapeDataString(userId)}/password-ticket",
            content: null,
            cancellationToken);
        return await ReadAsync<PasswordTicketResponseDto>(response, cancellationToken);
    }

    /// <summary>Enables or disables logins for a tenant.</summary>
    public async Task<TenantSummaryResponseDto> SetAccessGateAsync(
        string tenantId, TenantAccessGateUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"{TenantPath(tenantId)}/access-gate", request, cancellationToken);
        return await ReadAsync<TenantSummaryResponseDto>(response, cancellationToken);
    }

    /// <summary>Adds a location to a tenant.</summary>
    public async Task<TenantLocationProvisioningResponseDto> AddLocationAsync(
        string tenantId, TenantLocationCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"{TenantPath(tenantId)}/locations", request, cancellationToken);
        return await ReadAsync<TenantLocationProvisioningResponseDto>(response, cancellationToken);
    }

    private static string TenantPath(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return $"{BasePath}/{Uri.EscapeDataString(tenantId)}";
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await AdminApiException.FromResponseAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty response.");
    }
}

/// <summary>
/// A non-success response from the admin API, carrying the server's safe error message
/// (<c>ProblemDetails.detail</c>) so the page can show why a request was rejected.
/// </summary>
public sealed class AdminApiException : Exception
{
    private AdminApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>The caller is not a platform admin (Spec P-7).</summary>
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    internal static async Task<AdminApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var message = response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You are not authorized to use the admin tool.",
            _ => $"Request failed ({(int)response.StatusCode})."
        };

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in new[] { "detail", "message", "title" })
                    {
                        if (document.RootElement.TryGetProperty(property, out var value)
                            && value.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(value.GetString()))
                        {
                            message = value.GetString()!;
                            break;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON — keep the status-based message.
        }

        return new AdminApiException(response.StatusCode, message);
    }
}
