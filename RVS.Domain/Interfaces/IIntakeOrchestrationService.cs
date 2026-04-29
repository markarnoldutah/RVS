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
    /// <returns>
    /// A tuple containing the created <see cref="ServiceRequest"/> entity and the magic-link token
    /// (generated or reused) for checking request status.
    /// </returns>
    /// <exception cref="KeyNotFoundException">Thrown when the slug cannot be resolved.</exception>
    Task<(ServiceRequest ServiceRequest, string? MagicLinkToken)> ExecuteAsync(string slug, ServiceRequestCreateRequestDto request, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Assesses whether the location identified by <paramref name="slug"/> has the service
    /// capabilities typically required to address the customer's issue.
    /// <para>
    /// Resolves an issue category from the description via the AI categorization service
    /// (or accepts an already-resolved category from <paramref name="issueCategory"/>),
    /// looks up the required capability codes for that category, and compares them to the
    /// location's <see cref="Entities.Location.EnabledCapabilities"/>.
    /// </para>
    /// </summary>
    /// <param name="slug">Location slug.</param>
    /// <param name="issueDescription">Free-text description from Step 5 of the intake wizard.</param>
    /// <param name="issueCategory">
    /// Optional pre-resolved issue category. When supplied, the AI categorization step is skipped.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Assessment result including whether the location matches, the resolved category,
    /// the required capability codes, any missing capability codes, and the location phone
    /// number for use in customer-facing fallback messages.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="slug"/> or <paramref name="issueDescription"/> is null/whitespace.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the slug cannot be resolved.</exception>
    Task<CapabilityAssessmentResponseDto> AssessCapabilitiesAsync(
        string slug,
        string issueDescription,
        string? issueCategory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a location slug to the associated tenant identifier.
    /// </summary>
    /// <param name="slug">Location slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant identifier for the location.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the slug cannot be resolved.</exception>
    Task<string> ResolveSlugToTenantIdAsync(string slug, CancellationToken cancellationToken = default);
}
