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

    /// <summary>Photos sent per call when nothing is configured: the Spec A-6 attachment cap.</summary>
    public const int DefaultMaxImages = 5;

    /// <summary>Highest <see cref="MaxImages"/> honoured; anything above falls back to the default.</summary>
    public const int MaxImagesCeiling = 10;

    /// <summary>
    /// Most photos sent to the model in one assessment call (issue #772), bound from
    /// <c>AzureOpenAi:AssessmentMaxImages</c>. Every photo at detail "high" adds roughly 0.8–1.1K
    /// tokens reserved against the deployment's TPM limit, so an environment with less capacity can
    /// send fewer. Outside 1–<see cref="MaxImagesCeiling"/> it falls back to <see cref="DefaultMaxImages"/>.
    /// </summary>
    public int MaxImages { get; set; } = DefaultMaxImages;
}
