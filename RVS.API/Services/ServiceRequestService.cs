using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="ServiceRequest"/> entities.
/// Provides search, CRUD, status transitions, batch outcome, and delete operations.
/// </summary>
public sealed class ServiceRequestService : IServiceRequestService
{
    private readonly IServiceRequestRepository _repository;
    private readonly IUserContextAccessor _userContext;

    private const int MaxBatchSize = 25;

    /// <summary>
    /// Initializes a new instance of <see cref="ServiceRequestService"/>.
    /// </summary>
    public ServiceRequestService(IServiceRequestRepository repository, IUserContextAccessor userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{id}' not found.");
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

        return await _repository.SearchAsync(tenantId, request, continuationToken, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> CreateAsync(string tenantId, ServiceRequest entity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entity);

        return await _repository.CreateAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> UpdateAsync(string tenantId, string id, ServiceRequestUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{id}' not found.");

        if (request.UpdatedAtUtc.HasValue && existing.UpdatedAtUtc.HasValue
            && request.UpdatedAtUtc.Value != existing.UpdatedAtUtc.Value)
        {
            throw new ArgumentException("Optimistic concurrency conflict: the service request has been modified since it was last read.");
        }

        if (!string.Equals(existing.Status, request.Status, StringComparison.Ordinal)
            && !StatusTransitions.IsValid(existing.Status, request.Status))
        {
            throw new ArgumentException($"Invalid status transition from '{existing.Status}' to '{request.Status}'.");
        }

        existing.ApplyUpdate(request, _userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> UpdateStatusAsync(string tenantId, string id, string newStatus, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);

        var existing = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{id}' not found.");

        if (!StatusTransitions.IsValid(existing.Status, newStatus))
        {
            throw new ArgumentException($"Invalid status transition from '{existing.Status}' to '{newStatus}'.");
        }

        existing.Status = newStatus;
        existing.MarkAsUpdated(_userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BatchOutcomeResponseDto> BatchOutcomeAsync(string tenantId, BatchOutcomeRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ServiceRequestIds);

        if (request.ServiceRequestIds.Count > MaxBatchSize)
        {
            throw new ArgumentException($"Batch size exceeds maximum of {MaxBatchSize}.");
        }

        if (request.ServiceRequestIds.Count == 0)
        {
            throw new ArgumentException("At least one service request ID is required.");
        }

        var succeeded = new List<string>();
        var failed = new List<BatchOutcomeFailureDto>();

        foreach (var srId in request.ServiceRequestIds)
        {
            try
            {
                var sr = await _repository.GetByIdAsync(tenantId, srId, cancellationToken);

                if (sr is null)
                {
                    failed.Add(new BatchOutcomeFailureDto
                    {
                        ServiceRequestId = srId,
                        Reason = $"Service request '{srId}' not found."
                    });
                    continue;
                }

                if (!string.Equals(sr.TenantId, tenantId, StringComparison.Ordinal))
                {
                    failed.Add(new BatchOutcomeFailureDto
                    {
                        ServiceRequestId = srId,
                        Reason = $"Service request '{srId}' does not belong to tenant '{tenantId}'."
                    });
                    continue;
                }

                sr.ServiceEvent ??= new ServiceEventEmbedded();
                sr.ServiceEvent.FailureMode = request.FailureMode ?? sr.ServiceEvent.FailureMode;
                sr.ServiceEvent.RepairAction = request.RepairAction ?? sr.ServiceEvent.RepairAction;
                sr.ServiceEvent.PartsUsed = request.PartsUsed ?? sr.ServiceEvent.PartsUsed;
                sr.ServiceEvent.LaborHours = request.LaborHours ?? sr.ServiceEvent.LaborHours;
                sr.MarkAsUpdated(_userContext.UserId);

                await _repository.UpdateAsync(sr, cancellationToken);
                succeeded.Add(srId);
            }
            catch (Exception ex)
            {
                failed.Add(new BatchOutcomeFailureDto
                {
                    ServiceRequestId = srId,
                    Reason = ex.Message
                });
            }
        }

        return new BatchOutcomeResponseDto
        {
            Succeeded = succeeded,
            Failed = failed
        };
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _ = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{id}' not found.");

        await _repository.DeleteAsync(tenantId, id, cancellationToken);
    }
}
