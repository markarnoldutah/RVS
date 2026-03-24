using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using System.Net;

namespace RVS.Infra.AzCosmosRepository.Repositories;

/// <summary>
/// Cosmos DB repository for <see cref="ServiceRequest"/> entities.
/// Container: <c>service-requests</c>. Partition key: <c>/tenantId</c>.
/// </summary>
public sealed class CosmosServiceRequestRepository : CosmosRepositoryBase, IServiceRequestRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosServiceRequestRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosServiceRequestRepository"/>.
    /// </summary>
    public CosmosServiceRequestRepository(
        CosmosClient client,
        string databaseId,
        ILogger<CosmosServiceRequestRepository> logger) : base(client)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _container = GetContainer(databaseId, "service-requests");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServiceRequest?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var response = await _container.ReadItemAsync<ServiceRequest>(
                id,
                new PartitionKey(tenantId),
                cancellationToken: cancellationToken);

            _logger.LogDebug("GetByIdAsync [{Id}] — RequestCharge: {Charge} RU", id, response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceRequest>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @tenantId AND c.locationId = @locationId AND c.type = 'serviceRequest'")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@locationId", locationId);

        return await ExecuteQueryAsync(query, tenantId, nameof(GetByLocationAsync), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ServiceRequest>> SearchAsync(
        string tenantId,
        ServiceRequestSearchRequestDto request,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var pageSize = Math.Min(request.PageSize, 100);

        // Build dynamic WHERE clause — only include active filters
        var conditions = new List<string> { "c.tenantId = @tenantId", "c.type = 'serviceRequest'" };

        if (!string.IsNullOrWhiteSpace(request.Status))
            conditions.Add("c.status = @status");
        if (!string.IsNullOrWhiteSpace(request.IssueCategory))
            conditions.Add("c.issueCategory = @issueCategory");
        if (!string.IsNullOrWhiteSpace(request.LocationId))
            conditions.Add("c.locationId = @locationId");
        if (!string.IsNullOrWhiteSpace(request.AssignedTechnicianId))
            conditions.Add("c.assignedTechnicianId = @assignedTechnicianId");
        if (!string.IsNullOrWhiteSpace(request.AssignedBayId))
            conditions.Add("c.assignedBayId = @assignedBayId");
        if (!string.IsNullOrWhiteSpace(request.AssetId))
            conditions.Add("c.assetInfo.vin = @assetId");
        if (request.DateFrom.HasValue)
            conditions.Add("c.createdAtUtc >= @dateFrom");
        if (request.DateTo.HasValue)
            conditions.Add("c.createdAtUtc <= @dateTo");
        if (!string.IsNullOrWhiteSpace(request.Priority))
            conditions.Add("c.priority = @priority");
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            conditions.Add("(CONTAINS(LOWER(c.customerSnapshot.firstName), LOWER(@keyword)) OR CONTAINS(LOWER(c.customerSnapshot.lastName), LOWER(@keyword)) OR CONTAINS(LOWER(c.issueDescription), LOWER(@keyword)) OR CONTAINS(LOWER(c.assetInfo.vin), LOWER(@keyword)))");

        var sql = $"SELECT * FROM c WHERE {string.Join(" AND ", conditions)} ORDER BY c.createdAtUtc DESC";

        var definition = new QueryDefinition(sql)
            .WithParameter("@tenantId", tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
            definition = definition.WithParameter("@status", request.Status);
        if (!string.IsNullOrWhiteSpace(request.IssueCategory))
            definition = definition.WithParameter("@issueCategory", request.IssueCategory);
        if (!string.IsNullOrWhiteSpace(request.LocationId))
            definition = definition.WithParameter("@locationId", request.LocationId);
        if (!string.IsNullOrWhiteSpace(request.AssignedTechnicianId))
            definition = definition.WithParameter("@assignedTechnicianId", request.AssignedTechnicianId);
        if (!string.IsNullOrWhiteSpace(request.AssignedBayId))
            definition = definition.WithParameter("@assignedBayId", request.AssignedBayId);
        if (!string.IsNullOrWhiteSpace(request.AssetId))
            definition = definition.WithParameter("@assetId", request.AssetId);
        if (request.DateFrom.HasValue)
            definition = definition.WithParameter("@dateFrom", request.DateFrom.Value);
        if (request.DateTo.HasValue)
            definition = definition.WithParameter("@dateTo", request.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(request.Priority))
            definition = definition.WithParameter("@priority", request.Priority);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            definition = definition.WithParameter("@keyword", request.Keyword);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(tenantId),
            MaxItemCount = pageSize,
        };

        var iterator = _container.GetItemQueryIterator<ServiceRequest>(
            definition,
            requestOptions: queryOptions,
            continuationToken: continuationToken);

        var items = new List<ServiceRequest>();
        double totalCharge = 0;
        string? nextToken = null;

        if (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            nextToken = page.ContinuationToken;
            items.AddRange(page);
        }

        _logger.LogDebug("SearchAsync [tenant={TenantId}] — {Count} items, RequestCharge: {Charge} RU",
            tenantId, items.Count, totalCharge);

        // TotalCount is not available without an additional COUNT(*) query; it reflects only the current page.
        // Callers should use ContinuationToken to determine whether more results exist.
        return new PagedResult<ServiceRequest>
        {
            Page = request.Page,
            PageSize = pageSize,
            TotalCount = items.Count,
            Items = items,
            ContinuationToken = nextToken,
        };
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> CreateAsync(ServiceRequest entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));

        var response = await _container.CreateItemAsync(
            entity,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("CreateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> UpdateAsync(ServiceRequest entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TenantId, nameof(entity.TenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id, nameof(entity.Id));

        var response = await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(entity.TenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("UpdateAsync [{Id}] — RequestCharge: {Charge} RU", entity.Id, response.RequestCharge);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await _container.DeleteItemAsync<ServiceRequest>(
            id,
            new PartitionKey(tenantId),
            cancellationToken: cancellationToken);

        _logger.LogDebug("DeleteAsync [{Id}] — RequestCharge: {Charge} RU", id, response.RequestCharge);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private async Task<IReadOnlyList<ServiceRequest>> ExecuteQueryAsync(
        QueryDefinition query,
        string tenantId,
        string operationName,
        CancellationToken cancellationToken)
    {
        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) };
        var iterator = _container.GetItemQueryIterator<ServiceRequest>(query, requestOptions: options);

        var results = new List<ServiceRequest>();
        double totalCharge = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            totalCharge += page.RequestCharge;
            results.AddRange(page);
        }

        _logger.LogDebug("{Operation} [tenant={TenantId}] — {Count} items, RequestCharge: {Charge} RU",
            operationName, tenantId, results.Count, totalCharge);

        return results;
    }
}
