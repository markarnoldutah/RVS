namespace RVS.Domain.DTOs;

/// <summary>
/// Request to apply outcome fields to multiple service requests in a single batch.
/// </summary>
public sealed record BatchOutcomeRequestDto
{
    public required List<string> ServiceRequestIds { get; init; }
    public string? FailureMode { get; init; }
    public string? RepairAction { get; init; }
    public List<string>? PartsUsed { get; init; }
    public decimal? LaborHours { get; init; }
}
