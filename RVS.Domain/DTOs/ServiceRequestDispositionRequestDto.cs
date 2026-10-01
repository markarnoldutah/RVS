namespace RVS.Domain.DTOs;

/// <summary>
/// Request body for closing a service request without work (<c>Spec C-4</c>). The request moves
/// to <c>Cancelled</c> and the reason is stored on it. <see cref="ReasonCode"/> must be exactly one
/// of <c>DispositionReasons.All</c> — <c>Duplicate</c>, <c>Spam</c>, <c>WrongLocation</c>,
/// <c>CustomerWithdrew</c> — otherwise the API returns <c>422</c>.
/// </summary>
public sealed record ServiceRequestDispositionRequestDto
{
    /// <summary>The disposition reason code.</summary>
    public string ReasonCode { get; init; } = default!;
}
