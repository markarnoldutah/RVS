namespace RVS.API.Integrations;

/// <summary>
/// Request-shape settings for <see cref="AzureOpenAiPreliminaryAssessmentService"/>.
/// </summary>
public sealed class AzureOpenAiAssessmentOptions
{
    /// <summary>
    /// True when the assessment runs on its own dedicated deployment (issue #584), which is a
    /// reasoning model (gpt-5): the request then carries <c>max_completion_tokens</c> and
    /// <c>reasoning_effort</c> and no <c>temperature</c>. False when it falls back to the gpt-4o
    /// text deployment, which takes the classic <c>max_tokens</c> / <c>temperature</c> shape and
    /// rejects <c>reasoning_effort</c>.
    /// </summary>
    public bool UseReasoningModelRequest { get; set; }
}
