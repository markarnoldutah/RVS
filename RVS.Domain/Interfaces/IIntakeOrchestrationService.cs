using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Orchestrates the 7-step intake sequence that creates up to 5 Cosmos documents
/// (GlobalCustomerAcct, CustomerProfile, ServiceRequest, AssetLedgerEntry, updated linkages)
/// in a single intake request.
/// </summary>
public interface IIntakeOrchestrationService
{
    /// <summary>
    /// Executes the full intake orchestration sequence:
    /// <list type="number">
    ///   <item>Resolve slug → tenantId + locationId</item>
    ///   <item>Resolve GlobalCustomerAcct by email (create if absent)</item>
    ///   <item>Resolve CustomerProfile within tenant (create if absent) + asset ownership</item>
    ///   <item>Create ServiceRequest with customer snapshot, AI categorization, and technician summary</item>
    ///   <item>Append AssetLedgerEntry (non-blocking on failure)</item>
    ///   <item>Update linkages (increment requestCount, rotate magic-link token)</item>
    ///   <item>Fire-and-forget notification</item>
    /// </list>
    /// </summary>
    /// <param name="slug">Location slug for resolving tenant and location.</param>
    /// <param name="request">The service request creation DTO from the intake form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created <see cref="ServiceRequest"/> entity.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the slug cannot be resolved.</exception>
    Task<ServiceRequest> ExecuteAsync(string slug, ServiceRequestCreateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the location slug and assembles the intake configuration DTO,
    /// including dealership name, location details, accepted file types, and
    /// optionally prefilled customer data from a magic-link token.
    /// </summary>
    /// <param name="slug">Location slug for resolving tenant and location.</param>
    /// <param name="magicLinkToken">Optional magic-link token for customer prefill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The intake configuration for rendering the customer form.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the slug cannot be resolved.</exception>
    Task<IntakeConfigResponseDto> GetIntakeConfigAsync(string slug, string? magicLinkToken = null, CancellationToken cancellationToken = default);
}
