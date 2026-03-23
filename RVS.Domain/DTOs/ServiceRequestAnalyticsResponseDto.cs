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
    public List<AnalyticsRankItem> TopFailureModes { get; init; } = [];
    public List<AnalyticsRankItem> TopRepairActions { get; init; } = [];
    public decimal? AverageRepairTimeHours { get; init; }
    public List<AnalyticsRankItem> TopPartsUsed { get; init; } = [];
    public decimal? AverageDaysToComplete { get; init; }
}

/// <summary>
/// A ranked item with a name and count, used in analytics aggregations.
/// </summary>
public sealed record AnalyticsRankItem(string Name, int Count);
