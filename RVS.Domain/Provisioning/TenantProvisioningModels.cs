using RVS.Domain.Entities;
using RVS.Domain.Integrations;

namespace RVS.Domain.Provisioning;

/// <summary>Status values for one step of a provisioning run (Spec P-6, issue #563).</summary>
public static class ProvisioningStepStatus
{
    public const string Created = "created";
    public const string AlreadyExisted = "already existed";
    public const string Failed = "failed";

    /// <summary>Not attempted because an earlier step failed.</summary>
    public const string Skipped = "skipped";
}

/// <summary>Step names of a create-tenant run, in execution order (Spec P-1).</summary>
public static class ProvisioningStepNames
{
    public const string Tenant = "tenant";
    public const string TenantConfig = "tenant-config";
    public const string Dealership = "dealership";
    public const string Location = "location";
    public const string SlugLookup = "slug-lookup";
    public const string IdentityUser = "identity-user";
}

/// <summary>One step of a provisioning run.</summary>
/// <param name="Name">A <see cref="ProvisioningStepNames"/> value.</param>
/// <param name="Status">A <see cref="ProvisioningStepStatus"/> value.</param>
/// <param name="Message">Why the step failed; null otherwise.</param>
public sealed record ProvisioningStep(string Name, string Status, string? Message = null);

/// <summary>A tenant as the admin list shows it: commercial record, access gate, locations and the
/// tenant's master capability list (issue #757).</summary>
public sealed record TenantOverview(
    Tenant Tenant,
    TenantAccessGateEmbedded AccessGate,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<TenantCapabilityEmbedded> AvailableCapabilities);

/// <summary>Outcome of a create-tenant run (Spec P-1 / P-6).</summary>
/// <param name="TenantId">The tenant id used, derived or supplied.</param>
/// <param name="Steps">Every step in execution order.</param>
/// <param name="Location">The first location, when the location step completed.</param>
/// <param name="UserId">The first user's Auth0 id, when the identity step completed.</param>
/// <param name="PasswordTicket">The first user's set-password link, when the identity step completed.</param>
public sealed record TenantProvisioningResult(
    string TenantId,
    IReadOnlyList<ProvisioningStep> Steps,
    Location? Location,
    string? UserId,
    PasswordTicket? PasswordTicket)
{
    /// <summary>True when every step was created or already existed.</summary>
    public bool Succeeded => Steps.All(s => s.Status is ProvisioningStepStatus.Created or ProvisioningStepStatus.AlreadyExisted);
}

/// <summary>Outcome of adding a user to a tenant (Spec P-2).</summary>
public sealed record TenantUserProvisioningResult(
    string TenantId,
    string UserId,
    string Email,
    string Role,
    bool Created,
    PasswordTicket PasswordTicket);
