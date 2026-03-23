namespace RVS.Domain.DTOs;

/// <summary>
/// Lightweight access gate status for a tenant.
/// </summary>
public sealed record AccessGateStatusDto
{
    public bool IsEnabled { get; init; }
    public string? DisabledReason { get; init; }
    public string? DisabledMessage { get; init; }
}
