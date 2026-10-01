namespace RVS.Domain.DTOs;

/// <summary>
/// Outcome of a create-tenant submission (Spec P-1 / P-6 / P-8, issue #563). Every step is
/// reported as <c>created</c>, <c>already existed</c>, <c>failed</c> or <c>skipped</c> (not
/// attempted because an earlier step failed); re-submitting finishes a partial run.
/// </summary>
public sealed record TenantProvisioningResponseDto
{
    public string TenantId { get; init; } = string.Empty;

    /// <summary>True when every step was created or already existed.</summary>
    public bool Succeeded { get; init; }

    public List<ProvisioningStepDto> Steps { get; init; } = [];

    public string? LocationId { get; init; }
    public string? LocationSlug { get; init; }

    /// <summary>Public intake URL for the first location. Null when the location step did not complete.</summary>
    public string? IntakeUrl { get; init; }

    public string? UserId { get; init; }

    /// <summary>Auth0 set-password link for the first user. Shown once; never stored or logged.</summary>
    public string? PasswordTicketUrl { get; init; }

    public DateTime? PasswordTicketExpiresAtUtc { get; init; }
}

/// <summary>One step of a provisioning run.</summary>
public sealed record ProvisioningStepDto
{
    /// <summary><c>tenant</c>, <c>tenant-config</c>, <c>dealership</c>, <c>location</c>, <c>slug-lookup</c> or <c>identity-user</c>.</summary>
    public string Step { get; init; } = string.Empty;

    /// <summary><c>created</c>, <c>already existed</c>, <c>failed</c> or <c>skipped</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Why the step failed. Null otherwise.</summary>
    public string? Message { get; init; }
}
