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
    /// Decodes a VIN using the NHTSA vPIC API via the backend.
    /// Returns the decoded manufacturer, model, and year, or <c>null</c> if the VIN could not be decoded.
    /// </summary>
    /// <param name="locationSlug">The location slug.</param>
    /// <param name="vin">17-character Vehicle Identification Number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded vehicle information, or <c>null</c> if the API returned 404.</returns>
    public async Task<VinDecodeResponseDto?> DecodeVinAsync(
        string locationSlug,
        string vin,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        var response = await _httpClient.GetAsync(
           $"api/intake/{Uri.EscapeDataString(locationSlug)}/decode-vin/{Uri.EscapeDataString(vin)}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<VinDecodeResponseDto>(
            cancellationToken: cancellationToken);
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
    /// Requests a time-limited SAS URL for direct client-to-blob upload of an attachment.
    /// The API validates content type and attachment count before issuing the URL.
    /// </summary>
    /// <param name="locationSlug">The location slug.</param>
    /// <param name="serviceRequestId">The service request to attach the file to.</param>
    /// <param name="fileName">Original file name (e.g. "photo.jpg").</param>
    /// <param name="contentType">MIME content type (e.g. "image/jpeg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SAS URL, blob name, and expiry time for direct upload.</returns>
    public async Task<AttachmentUploadSasResponseDto> GetUploadSasAsync(
        string locationSlug,
        string serviceRequestId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var url = $"api/intake/{Uri.EscapeDataString(locationSlug)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}/attachments/upload-url"
            + $"?fileName={Uri.EscapeDataString(fileName)}&contentType={Uri.EscapeDataString(contentType)}";

        var response = await _httpClient.PostAsync(url, content: null, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AttachmentUploadSasResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize upload SAS response.");
    }

    /// <summary>
    /// Confirms a direct-upload attachment after the client has uploaded the blob via SAS URL.
    /// </summary>
    /// <param name="locationSlug">The location slug.</param>
    /// <param name="serviceRequestId">The service request to attach the file to.</param>
    /// <param name="request">Confirmation details (blob name, file name, content type, size).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The confirmed attachment record.</returns>
    public async Task<AttachmentDto> ConfirmUploadAsync(
        string locationSlug,
        string serviceRequestId,
        AttachmentConfirmRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"api/intake/{Uri.EscapeDataString(locationSlug)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}/attachments/confirm",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AttachmentDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize attachment confirm response.");
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
