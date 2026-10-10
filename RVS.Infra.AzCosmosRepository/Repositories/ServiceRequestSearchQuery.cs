using Microsoft.Azure.Cosmos;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Builds the Cosmos SQL behind the service-request search (issue #849), kept apart from
/// <see cref="CosmosServiceRequestRepository"/> so the query can be tested without a container.
/// </summary>
/// <remarks>
/// The search applies every filter and returns the whole matching set rather than a page: the
/// manager app sorts and pages it. The list scope reads one row past <see cref="ServiceRequestSearch.MaxListResults"/>
/// with <c>TOP</c> so a truncated result is detectable without a second <c>COUNT</c> query.
/// Every caller value is a parameter; the only interpolated text is fixed vocabulary.
/// </remarks>
public static class ServiceRequestSearchQuery
{
    /// <summary>
    /// The top-level document fields the search projects: everything
    /// <c>ServiceRequestMapper.ToSummaryDto</c> reads, and nothing heavier (issue text, packet and
    /// AI state stay behind).
    /// </summary>
    public static IReadOnlyList<string> SummaryFields { get; } =
    [
        "id", "tenantId", "type", "locationId", "status", "disposition",
        "customerSnapshot", "assetInfo", "issueCategory", "technicianSummary", "attachments",
        "assignedTechnicianId", "priority", "jobType",
        "scheduledStartUtc", "scheduledTimeZone", "scheduledTimeIsSet",
        "boardSequence", "submissionId", "submissionPosition", "submissionCount",
        "createdAtUtc", "updatedAtUtc",
    ];

    private static readonly string ClosedStatusList =
        string.Join(", ", BoardRetentionFilter.ClosedStatuses.Select(s => $"'{s}'"));

    private static readonly string Projection = string.Join(", ", SummaryFields.Select(f => $"c.{f}"));

    /// <summary>Builds the search query for <paramref name="request"/>.</summary>
    /// <param name="tenantId">Tenant partition key.</param>
    /// <param name="request">Scope and filter criteria.</param>
    /// <param name="nowUtc">The current time (UTC); fixes the board's Done-window cutoff.</param>
    public static QueryDefinition Build(string tenantId, ServiceRequestSearchRequestDto request, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var isBoard = ServiceRequestSearch.IsBoardScope(request.Scope);
        var conditions = new List<string> { "c.tenantId = @tenantId", "c.type = 'serviceRequest'" };
        var parameters = new List<(string Name, object Value)> { ("@tenantId", tenantId) };

        if (isBoard)
        {
            // Spec C-10, as BoardRetentionFilter.IsOnBoard: open work always; closed work while it
            // was last changed inside the Done window, falling back to the created date.
            conditions.Add(
                $"(c.status NOT IN ({ClosedStatusList})"
                + " OR (IS_DEFINED(c.updatedAtUtc) AND NOT IS_NULL(c.updatedAtUtc) AND c.updatedAtUtc > @doneCutoff)"
                + " OR ((NOT IS_DEFINED(c.updatedAtUtc) OR IS_NULL(c.updatedAtUtc)) AND c.createdAtUtc > @doneCutoff))");
            parameters.Add(("@doneCutoff", BoardRetentionFilter.DoneColumnCutoffUtc(nowUtc)));
        }

        if (string.Equals(request.Status, ServiceRequestSearch.OpenStatusFilter, StringComparison.Ordinal))
        {
            conditions.Add($"c.status NOT IN ({ClosedStatusList})");
        }
        else if (!string.IsNullOrWhiteSpace(request.Status))
        {
            conditions.Add("c.status = @status");
            parameters.Add(("@status", request.Status));
        }

        if (!string.IsNullOrWhiteSpace(request.LocationId))
        {
            conditions.Add("c.locationId = @locationId");
            parameters.Add(("@locationId", request.LocationId));
        }

        if (!string.IsNullOrWhiteSpace(request.IssueCategory))
        {
            conditions.Add("c.issueCategory = @issueCategory");
            parameters.Add(("@issueCategory", IssueCategoryVocabulary.Normalize(request.IssueCategory)));
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            conditions.Add("c.priority = @priority");
            parameters.Add(("@priority", request.Priority));
        }

        if (!string.IsNullOrWhiteSpace(request.AssignedTechnicianId))
        {
            conditions.Add("CONTAINS(LOWER(c.assignedTechnicianId), LOWER(@assignedTechnicianId))");
            parameters.Add(("@assignedTechnicianId", request.AssignedTechnicianId.Trim()));
        }

        if (request.DateFrom.HasValue)
        {
            conditions.Add("c.createdAtUtc >= @dateFrom");
            parameters.Add(("@dateFrom", request.DateFrom.Value));
        }

        if (request.DateTo.HasValue)
        {
            conditions.Add("c.createdAtUtc <= @dateTo");
            parameters.Add(("@dateTo", request.DateTo.Value));
        }

        if (string.Equals(request.JobType, JobTypes.NotTriagedFilter, StringComparison.Ordinal))
        {
            conditions.Add("(NOT IS_DEFINED(c.jobType) OR IS_NULL(c.jobType))");
        }
        else if (!string.IsNullOrWhiteSpace(request.JobType))
        {
            conditions.Add("c.jobType = @jobType");
            parameters.Add(("@jobType", request.JobType));
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            conditions.Add("(CONTAINS(LOWER(c.customerSnapshot.firstName), LOWER(@keyword)) OR CONTAINS(LOWER(c.customerSnapshot.lastName), LOWER(@keyword)) OR CONTAINS(LOWER(c.issueDescription), LOWER(@keyword)) OR CONTAINS(LOWER(c.assetInfo.assetId), LOWER(@keyword)))");
            parameters.Add(("@keyword", request.Keyword));
        }

        var top = isBoard ? string.Empty : $"TOP {ServiceRequestSearch.MaxListResults + 1} ";
        var sql = $"SELECT {top}{Projection} FROM c WHERE {string.Join(" AND ", conditions)} ORDER BY c.createdAtUtc DESC";

        var definition = new QueryDefinition(sql);
        foreach (var (name, value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        return definition;
    }

    /// <summary>
    /// Turns the rows a query read into the search result: the list scope keeps the first
    /// <see cref="ServiceRequestSearch.MaxListResults"/> and flags anything past them; the board
    /// scope keeps every row.
    /// </summary>
    /// <param name="rows">The rows read, newest first.</param>
    /// <param name="scope">The request's scope.</param>
    public static ServiceRequestSearchResult ToResult(IReadOnlyList<ServiceRequest> rows, string? scope)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (ServiceRequestSearch.IsBoardScope(scope) || rows.Count <= ServiceRequestSearch.MaxListResults)
        {
            return new ServiceRequestSearchResult { Items = rows };
        }

        return new ServiceRequestSearchResult
        {
            Items = rows.Take(ServiceRequestSearch.MaxListResults).ToList(),
            IsTruncated = true,
        };
    }
}
