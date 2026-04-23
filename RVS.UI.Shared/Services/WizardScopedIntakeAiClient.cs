using System.Net.Http.Json;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Anonymous, slug-scoped implementation of <see cref="IIntakeAiClient"/>.
/// Reads the location slug from <see cref="IIntakeWizardState.Slug"/> at call time
/// and routes each request to <c>api/intake/{slug}/*</c>, matching the existing
/// anonymous <c>IntakeController</c> surface (rate-limited by <c>IntakeEndpoint</c>).
/// </summary>
public sealed class WizardScopedIntakeAiClient : IIntakeAiClient
{
    private readonly HttpClient _httpClient;
    private readonly IIntakeWizardState _state;

    /// <summary>
    /// Initializes a new instance of <see cref="WizardScopedIntakeAiClient"/>.
    /// </summary>
    public WizardScopedIntakeAiClient(HttpClient httpClient, IIntakeWizardState state)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(state);
        _httpClient = httpClient;
        _state = state;
    }

    /// <inheritdoc />
    public async Task<VinDecodeResponseDto?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        var response = await _httpClient.GetAsync(
            $"api/intake/{EncodedSlug}/decode-vin/{Uri.EscapeDataString(vin)}",
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
            "ai/extract-vin", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueTranscriptionResultDto>?> TranscribeIssueAsync(
        IssueTranscriptionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueTranscriptionRequestDto, IssueTranscriptionResultDto>(
            "ai/transcribe-issue", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueTextRefinementResultDto>?> RefineIssueTextAsync(
        IssueTextRefinementRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueTextRefinementRequestDto, IssueTextRefinementResultDto>(
            "ai/refine-issue-text", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueCategorySuggestionResultDto>?> SuggestIssueCategoryAsync(
        IssueCategorySuggestionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueCategorySuggestionRequestDto, IssueCategorySuggestionResultDto>(
            "ai/suggest-category", request, cancellationToken);

    /// <inheritdoc />
    public Task<AiOperationResponseDto<IssueInsightsSuggestionResultDto>?> SuggestIssueInsightsAsync(
        IssueInsightsSuggestionRequestDto request, CancellationToken cancellationToken = default) =>
        PostAiAsync<IssueInsightsSuggestionRequestDto, IssueInsightsSuggestionResultDto>(
            "ai/suggest-insights", request, cancellationToken);

    private string EncodedSlug => Uri.EscapeDataString(_state.Slug);

    private async Task<AiOperationResponseDto<TResult>?> PostAiAsync<TRequest, TResult>(
        string relativeAction, TRequest request, CancellationToken cancellationToken)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _httpClient.PostAsJsonAsync(
            $"api/intake/{EncodedSlug}/{relativeAction}",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AiOperationResponseDto<TResult>>(
            cancellationToken: cancellationToken);
    }
}
