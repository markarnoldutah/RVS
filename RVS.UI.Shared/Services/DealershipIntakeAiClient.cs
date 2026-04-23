using System.Net.Http.Json;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Authenticated, dealership-scoped implementation of <see cref="IIntakeAiClient"/>.
/// Routes each request to <c>api/dealerships/{dealershipId}/intake/*</c>, hitting the
/// authenticated <c>DealerIntakeController</c>. Used by the Manager walk-in flow so AI calls
/// avoid the anonymous rate limiter and carry the manager's JWT for tenant enforcement.
///
/// Bearer token attachment is the responsibility of the <see cref="HttpClient"/> handler
/// chain supplied via DI (typically a <see cref="DelegatingHandler"/> that reads from
/// the Auth0 access-token provider).
/// </summary>
public sealed class DealershipIntakeAiClient : IIntakeAiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _dealershipId;

    /// <summary>
    /// Initializes a new instance of <see cref="DealershipIntakeAiClient"/>.
    /// </summary>
    /// <param name="httpClient">Authenticated <see cref="HttpClient"/> (bearer token already attached by handler chain).</param>
    /// <param name="dealershipId">Dealership identifier used as the route scope segment.</param>
    public DealershipIntakeAiClient(HttpClient httpClient, string dealershipId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(dealershipId);
        _httpClient = httpClient;
        _dealershipId = dealershipId;
    }

    /// <inheritdoc />
    public async Task<VinDecodeResponseDto?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        var response = await _httpClient.GetAsync(
            $"{BasePath}/decode-vin/{Uri.EscapeDataString(vin)}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VinDecodeResponseDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<AiOperationResponseDto<VinExtractionResultDto>?> ExtractVinFromImageAsync(
        VinExtractionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<VinExtractionRequestDto, VinExtractionResultDto>(
            "extract-vin", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueTranscriptionResultDto>?> TranscribeIssueAsync(
        IssueTranscriptionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueTranscriptionRequestDto, IssueTranscriptionResultDto>(
            "transcribe-issue", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueTextRefinementResultDto>?> RefineIssueTextAsync(
        IssueTextRefinementRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueTextRefinementRequestDto, IssueTextRefinementResultDto>(
            "refine-issue-text", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueCategorySuggestionResultDto>?> SuggestIssueCategoryAsync(
        IssueCategorySuggestionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueCategorySuggestionRequestDto, IssueCategorySuggestionResultDto>(
            "suggest-category", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueInsightsSuggestionResultDto>?> SuggestIssueInsightsAsync(
        IssueInsightsSuggestionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueInsightsSuggestionRequestDto, IssueInsightsSuggestionResultDto>(
            "suggest-insights", request, cancellationToken);

    private string BasePath => $"api/dealerships/{Uri.EscapeDataString(_dealershipId)}/intake";

    private async Task<AiOperationResponseDto<TResult>?> PostAiAsync<TRequest, TResult>(
        string action, TRequest request, CancellationToken cancellationToken)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"{BasePath}/{action}",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AiOperationResponseDto<TResult>>(
            cancellationToken: cancellationToken);
    }
}
