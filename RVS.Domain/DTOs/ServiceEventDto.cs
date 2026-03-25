namespace RVS.Domain.DTOs;

/// <summary>
/// Structured service event data for a service request.
/// </summary>
public sealed record ServiceEventDto
{
    public string? ComponentType { get; init; }
    public string? FailureMode { get; init; }
    public string? RepairAction { get; init; }
    public List<string> PartsUsed { get; init; } = [];
    public decimal? LaborHours { get; init; }
    public DateTime? ServiceDateUtc { get; init; }
}
