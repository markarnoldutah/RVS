using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for anonymous intake operations against the RVS API.
/// Routes map to <c>api/intake/{locationSlug}</c>.
/// </summary>
public sealed class IntakeApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public IntakeApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the intake form configuration for a location.
    /// </summary>
    /// <param name="locationSlug">The location slug.</param>
    /// <param name="token">Optional magic-link token for customer prefill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IntakeConfigResponseDto> GetConfigAsync(
        string locationSlug,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);

        var url = $"api/intake/{Uri.EscapeDataString(locationSlug)}/config";
        if (!string.IsNullOrWhiteSpace(token))
        {
            url += $"?token={Uri.EscapeDataString(token)}";
        }

        return await _httpClient.GetFromJsonAsync<IntakeConfigResponseDto>(url, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize intake config response.");
    }

    /// <summary>
    /// Requests AI-generated diagnostic questions for the intake wizard.
    /// </summary>
    public async Task<DiagnosticQuestionsResponseDto> GetDiagnosticQuestionsAsync(
        string locationSlug,
        ServiceRequestCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"api/intake/{Uri.EscapeDataString(locationSlug)}/diagnostic-questions",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DiagnosticQuestionsResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize diagnostic questions response.");
    }

    /// <summary>
    /// Submits a new service request through the intake flow.
    /// </summary>
    public async Task<ServiceRequestDetailResponseDto> SubmitServiceRequestAsync(
        string locationSlug,
        ServiceRequestCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"api/intake/{Uri.EscapeDataString(locationSlug)}/service-requests",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServiceRequestDetailResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize service request response.");
    }

    /// <summary>
    /// Uploads an attachment to an existing service request via the anonymous intake flow.
    /// </summary>
    public async Task<AttachmentDto> UploadAttachmentAsync(
        string locationSlug,
        string serviceRequestId,
        HttpContent fileContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentNullException.ThrowIfNull(fileContent);

        var response = await _httpClient.PostAsync(
            $"api/intake/{Uri.EscapeDataString(locationSlug)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}/attachments",
            fileContent,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AttachmentDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize attachment response.");
    }

    /// <summary>
    /// Gets the customer status page data using a magic-link token.
    /// </summary>
    public async Task<CustomerStatusResponseDto> GetStatusAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return await _httpClient.GetFromJsonAsync<CustomerStatusResponseDto>(
            $"api/status/{Uri.EscapeDataString(token)}",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize status response.");
    }
}
