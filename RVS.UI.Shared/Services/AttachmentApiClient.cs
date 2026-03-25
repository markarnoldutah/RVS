using System.Net.Http.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for attachment operations against the RVS API.
/// Routes map to <c>api/dealerships/{dealershipId}/service-requests/{srId}/attachments</c>.
/// </summary>
public sealed class AttachmentApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="AttachmentApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public AttachmentApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Uploads an attachment to a service request.
    /// </summary>
    public async Task<AttachmentDto> UploadAsync(
        string dealershipId,
        string serviceRequestId,
        HttpContent fileContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentNullException.ThrowIfNull(fileContent);

        var response = await _httpClient.PostAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}/attachments",
            fileContent,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AttachmentDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize attachment response.");
    }

    /// <summary>
    /// Gets a read-only SAS URL for an attachment.
    /// </summary>
    public async Task<AttachmentSasDto> GetReadSasAsync(
        string dealershipId,
        string serviceRequestId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

        return await _httpClient.GetFromJsonAsync<AttachmentSasDto>(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}/attachments/{Uri.EscapeDataString(attachmentId)}/sas",
            cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize SAS URL response.");
    }

    /// <summary>
    /// Deletes an attachment from a service request.
    /// </summary>
    public async Task DeleteAsync(
        string dealershipId,
        string serviceRequestId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

        var response = await _httpClient.DeleteAsync(
            $"api/dealerships/{Uri.EscapeDataString(dealershipId)}/service-requests/{Uri.EscapeDataString(serviceRequestId)}/attachments/{Uri.EscapeDataString(attachmentId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
