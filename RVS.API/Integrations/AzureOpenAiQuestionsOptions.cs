namespace RVS.API.Integrations;

/// <summary>
/// Request-shape settings for diagnostic question generation in
/// <see cref="AzureOpenAiCategorizationService"/> (issue #783). The other calls on that service
/// are unaffected.
/// </summary>
public sealed class AzureOpenAiQuestionsOptions
{
    /// <summary>Effort sent when nothing valid is configured.</summary>
    public const string DefaultReasoningEffort = "minimal";

    /// <summary>
    /// True when question generation runs on its own deployment (<c>AzureOpenAi:QuestionsDeploymentName</c>),
    /// which is a reasoning model (gpt-5): the request then carries <c>max_completion_tokens</c> and
    /// <c>reasoning_effort</c>. False keeps it on the gpt-4o text deployment with the classic
    /// <c>max_tokens</c> shape, exactly as before.
    /// </summary>
    public bool UseReasoningModelRequest { get; set; }

    /// <summary>
    /// <c>minimal</c> or <c>low</c>, bound from <c>AzureOpenAi:QuestionsReasoningEffort</c>. The call is
    /// on the customer's path at step 6, so anything slower is not allowed; any other value falls
    /// back to <see cref="DefaultReasoningEffort"/>.
    /// </summary>
    public string? ReasoningEffort { get; set; } = DefaultReasoningEffort;
}
