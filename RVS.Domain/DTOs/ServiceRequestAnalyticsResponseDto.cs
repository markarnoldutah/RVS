namespace RVS.Domain.DTOs;

/// <summary>
/// Aggregated analytics for service requests within a tenant.
/// </summary>
public sealed record ServiceRequestAnalyticsResponseDto
{
    public int TotalRequests { get; init; }
    public Dictionary<string, int> RequestsByStatus { get; init; } = new();
    public Dictionary<string, int> RequestsByCategory { get; init; } = new();
    public Dictionary<string, int> RequestsByLocation { get; init; } = new();
    public decimal? AverageDaysToComplete { get; init; }
}
