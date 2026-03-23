namespace RVS.Domain.Validation;

/// <summary>
/// Represents the result of a domain validation operation.
/// </summary>
/// <param name="IsValid">Whether the validation passed.</param>
/// <param name="ErrorMessage">A descriptive error message when validation fails; null when valid.</param>
public sealed record ValidationResult(bool IsValid, string? ErrorMessage = null)
{
    /// <summary>Returns a successful validation result.</summary>
    public static readonly ValidationResult Success = new(true);

    /// <summary>Returns a failed validation result with the specified error message.</summary>
    /// <param name="errorMessage">A descriptive error message explaining the failure.</param>
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
