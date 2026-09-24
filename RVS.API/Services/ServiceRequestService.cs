using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="ServiceRequest"/> entities.
/// Provides search, CRUD, status transitions, and delete operations.
/// </summary>
public sealed class ServiceRequestService : IServiceRequestService
{
    private readonly IServiceRequestRepository _repository;
    private readonly IUserContextAccessor _userContext;
    private readonly IPacketGenerationService _packetGenerationService;

    /// <summary>
    /// Initializes a new instance of <see cref="ServiceRequestService"/>.
    /// </summary>
    public ServiceRequestService(
        IServiceRequestRepository repository,
        IUserContextAccessor userContext,
        IPacketGenerationService packetGenerationService)
    {
        _repository = repository;
        _userContext = userContext;
        _packetGenerationService = packetGenerationService;
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
        existing.ClearDispositionIfReopened();
        existing.MarkAsUpdated(_userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
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

    /// <inheritdoc />
    public async Task<ServiceRequest> SetCustomerStatusNoteAsync(string tenantId, string id, string? note, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // C-9: validate before touching the store. The note text is never included in the
        // exception message, an activity tag, or a log entry.
        var validation = CustomerStatusNoteValidator.Validate(note);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage, nameof(note));
        }

        var existing = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{id}' not found.");

        existing.SetCustomerStatusNote(note, _userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceRequest> CloseWithDispositionAsync(string tenantId, string id, string reasonCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!DispositionReasons.IsValid(reasonCode))
        {
            throw new ArgumentException($"Unknown disposition reason '{reasonCode}'.", nameof(reasonCode));
        }

        var existing = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request '{id}' not found.");

        existing.CloseWithDisposition(reasonCode, _userContext.UserId);

        return await _repository.UpdateAsync(existing, cancellationToken);
    }

    /// <inheritdoc />
    public Task RegeneratePacketAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _packetGenerationService.RequestRegenerationAsync(tenantId, id, cancellationToken);
    }
}
