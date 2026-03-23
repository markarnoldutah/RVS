namespace RVS.Domain.DTOs;

/// <summary>
/// Result of a batch outcome operation, listing succeeded and failed items.
/// </summary>
public sealed record BatchOutcomeResponseDto
{
    public List<string> Succeeded { get; init; } = [];
    public List<BatchOutcomeFailureDto> Failed { get; init; } = [];
}

/// <summary>
/// Details about a single service request that failed during a batch outcome operation.
/// </summary>
public sealed record BatchOutcomeFailureDto
{
    public required string ServiceRequestId { get; init; }
    public required string Reason { get; init; }
}
