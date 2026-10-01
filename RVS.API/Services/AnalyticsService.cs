using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Computes analytics rollups from service request data for the manager dashboard.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IServiceRequestRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="AnalyticsService"/>.
    /// </summary>
    public AnalyticsService(IServiceRequestRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ServiceRequestAnalyticsResponseDto> GetServiceRequestSummaryAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        string? locationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var requests = await _repository.GetForAnalyticsAsync(tenantId, from, to, locationId, cancellationToken);

        return BuildSummary(requests);
    }

    /// <summary>
    /// Builds the analytics response DTO from a collection of service requests.
    /// </summary>
    private static ServiceRequestAnalyticsResponseDto BuildSummary(IReadOnlyList<ServiceRequest> requests)
    {
        if (requests.Count == 0)
        {
            return new ServiceRequestAnalyticsResponseDto();
        }

        var requestsByStatus = requests
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var requestsByCategory = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.IssueCategory))
            .GroupBy(r => r.IssueCategory!)
            .ToDictionary(g => g.Key, g => g.Count());

        var requestsByLocation = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.LocationId))
            .GroupBy(r => r.LocationId)
            .ToDictionary(g => g.Key, g => g.Count());

        var completedRequests = requests
            .Where(r => r.Status is "Completed" && r.UpdatedAtUtc.HasValue)
            .Select(r => (r.UpdatedAtUtc!.Value - r.CreatedAtUtc).TotalDays)
            .ToList();

        decimal? averageDaysToComplete = completedRequests.Count > 0
            ? Math.Round((decimal)completedRequests.Average(), 2)
            : null;

        return new ServiceRequestAnalyticsResponseDto
        {
            TotalRequests = requests.Count,
            RequestsByStatus = requestsByStatus,
            RequestsByCategory = requestsByCategory,
            RequestsByLocation = requestsByLocation,
            AverageDaysToComplete = averageDaysToComplete,
        };
    }
}
