namespace RVS.Domain.DTOs;

/// <summary>
/// Standardized envelope for all AI operation responses in the intake workflow.
/// Wraps the typed result with metadata required by Wave 1 AI endpoints.
/// </summary>
/// <typeparam name="T">The typed result payload specific to each AI operation.</typeparam>
/// <example>
/// <code>
/// var response = new AiOperationResponseDto&lt;VinExtractionResultDto&gt;
/// {
///     Success = true,
///     Result = new VinExtractionResultDto { Vin = "1HGBH41JXMN109186" },
///     Confidence = 0.95,
///     Warnings = [],
///     Provider = "AzureOpenAiVinExtractionService",
///     CorrelationId = "abc123"
/// };
/// </code>
/// </example>
public sealed record AiOperationResponseDto<T>
{
    /// <summary>
    /// Indicates whether the AI operation completed without a hard failure.
    /// A <c>true</c> value does not guarantee a result was found — the <see cref="Result"/>
    /// may still be <c>null</c> when the provider found nothing actionable.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The typed payload returned by the AI operation, or <c>null</c> when the
    /// operation succeeded but produced no actionable output (e.g. no VIN detected).
    /// </summary>
    public T? Result { get; init; }

    /// <summary>
    /// Confidence score in the range <c>0.0</c> to <c>1.0</c>.
    /// Consumers should apply a threshold before auto-filling UI fields.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Non-blocking advisory messages produced during the operation
    /// (e.g. low-confidence warnings, partial parse issues).
    /// Never <c>null</c>; empty when no warnings exist.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Identifies the provider or service implementation that fulfilled the request,
    /// e.g. <c>"AzureOpenAiVinExtractionService"</c> or <c>"MockVinExtractionService"</c>.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Correlation identifier used to trace the request across API logs and telemetry.
    /// Matches the value propagated by <c>CorrelationLoggingMiddleware</c>.
    /// </summary>
    public required string CorrelationId { get; init; }
}
