using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// Input rules for the platform-admin provisioning tool (Spec P-1 … P-5, issue #563). Each
/// method returns the first problem found, or <see cref="ValidationResult.Success"/>.
/// </summary>
public static partial class TenantProvisioningValidator
{
    /// <summary>Tenant id carried by RVS staff accounts. Never provisionable.</summary>
    public const string PlatformTenantId = "org_rvs_platform";

    /// <summary>The one tenant-wide role; every other provisionable role is location-scoped.</summary>
    public const string OwnerRole = "dealer:owner";

    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 254;
    private const int MaxNotesLength = 2000;
    private const int MaxPhoneLength = 30;
    private const int MaxReasonLength = 200;
    private const int MaxLocationIds = 50;
    private const int MaxLocationIdLength = 100;

    private static readonly EmailAddressAttribute EmailValidator = new();

    /// <summary>
    /// Roles the tool may assign (Spec P-2). Archived roles (<c>dealer:technician</c>,
    /// <c>dealer:regional-manager</c>, <c>dealer:corporate-admin</c>) and <c>platform:admin</c> are not offered.
    /// </summary>
    public static IReadOnlyList<string> ProvisionableRoles { get; } =
        [OwnerRole, "dealer:manager", "dealer:advisor", "dealer:readonly"];

    /// <summary>Commercial tenant states (Spec P-4). Independent of the access gate.</summary>
    public static IReadOnlyList<string> Statuses { get; } = ["Pilot", "Active", "Churned"];

    /// <summary>Billing plans (Spec P-1).</summary>
    public static IReadOnlyList<string> Plans { get; } = ["mobile", "location"];

    [GeneratedRegex("^org_[a-z0-9]+(?:_[a-z0-9]+)*$")]
    private static partial Regex TenantIdPattern();

    /// <summary>Whether <paramref name="role"/> is provisionable and scoped to specific locations.</summary>
    public static bool IsLocationScopedRole(string? role) =>
        role is not null
        && !string.Equals(role, OwnerRole, StringComparison.Ordinal)
        && ProvisionableRoles.Contains(role, StringComparer.Ordinal);

    /// <summary>The canonical spelling of a status (case-insensitive match), or <c>null</c> when unknown.</summary>
    public static string? CanonicalStatus(string? value) => Canonical(Statuses, value);

    /// <summary>The canonical spelling of a plan (case-insensitive match), or <c>null</c> when unknown.</summary>
    public static string? CanonicalPlan(string? value) => Canonical(Plans, value);

    /// <summary>
    /// Validates a tenant id: <c>org_</c> then lowercase letters and digits in single-underscore
    /// separated groups, at most <see cref="TenantIdGenerator.MaxLength"/> characters, and not
    /// the reserved <see cref="PlatformTenantId"/>.
    /// </summary>
    public static ValidationResult ValidateTenantId(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return ValidationResult.Failure("Tenant id is required.");
        }

        if (tenantId.Length > TenantIdGenerator.MaxLength)
        {
            return ValidationResult.Failure($"Tenant id must not exceed {TenantIdGenerator.MaxLength} characters.");
        }

        if (!TenantIdPattern().IsMatch(tenantId))
        {
            return ValidationResult.Failure(
                "Tenant id must look like 'org_shop_name': 'org_' then lowercase letters and digits separated by single underscores.");
        }

        if (string.Equals(tenantId, PlatformTenantId, StringComparison.Ordinal))
        {
            return ValidationResult.Failure($"Tenant id '{PlatformTenantId}' is reserved for RVS staff.");
        }

        return ValidationResult.Success;
    }

    /// <summary>Validates a create-tenant request (Spec P-1).</summary>
    public static ValidationResult ValidateCreateTenant(TenantCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return FirstFailure(
            () => RequiredText(request.Name, "Tenant name", MaxNameLength),
            () => string.IsNullOrWhiteSpace(request.TenantId) ? ValidationResult.Success : ValidateTenantId(request.TenantId.Trim()),
            () => OptionalEmail(request.BillingEmail, "Billing email"),
            () => CanonicalPlan(request.Plan) is null
                ? ValidationResult.Failure($"Plan must be one of: {string.Join(", ", Plans)}.")
                : ValidationResult.Success,
            () => MaxLength(request.Notes, "Notes", MaxNotesLength),
            () => RequiredText(request.LocationName, "Location name", MaxNameLength),
            () => OptionalSlug(request.LocationSlug),
            () => MaxLength(request.LocationPhone, "Phone", MaxPhoneLength),
            () => RequiredEmail(request.OwnerEmail, "First user's email"),
            () => RequiredText(request.OwnerDisplayName, "First user's name", MaxNameLength),
            () => ProvisionableRole(request.OwnerRole));
    }

    /// <summary>Validates an edit to a tenant's commercial details. Null fields are left unchanged.</summary>
    public static ValidationResult ValidateUpdateTenant(TenantUpdateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return FirstFailure(
            () => request.Status is not null && CanonicalStatus(request.Status) is null
                ? ValidationResult.Failure($"Status must be one of: {string.Join(", ", Statuses)}.")
                : ValidationResult.Success,
            () => request.Plan is not null && CanonicalPlan(request.Plan) is null
                ? ValidationResult.Failure($"Plan must be one of: {string.Join(", ", Plans)}.")
                : ValidationResult.Success,
            () => OptionalEmail(request.BillingEmail, "Billing email"),
            () => MaxLength(request.Notes, "Notes", MaxNotesLength));
    }

    /// <summary>Validates an add-user request (Spec P-2). Location-scoped roles need at least one location.</summary>
    public static ValidationResult ValidateAddUser(TenantUserCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return FirstFailure(
            () => RequiredEmail(request.Email, "Email"),
            () => RequiredText(request.DisplayName, "Name", MaxNameLength),
            () => ProvisionableRole(request.Role),
            () => IsLocationScopedRole(request.Role) ? LocationIds(request.LocationIds) : ValidationResult.Success);
    }

    /// <summary>Validates an access-gate change (Spec P-4). Disabling requires a reason.</summary>
    public static ValidationResult ValidateAccessGate(TenantAccessGateUpdateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.LoginsEnabled && string.IsNullOrWhiteSpace(request.Reason))
        {
            return ValidationResult.Failure("A reason is required when disabling logins.");
        }

        return string.IsNullOrWhiteSpace(request.Reason)
            ? ValidationResult.Success
            : RequiredText(request.Reason, "Reason", MaxReasonLength);
    }

    /// <summary>Validates an add-location request (Spec P-5): 1–10 packet recipients.</summary>
    public static ValidationResult ValidateAddLocation(TenantLocationCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return FirstFailure(
            () => RequiredText(request.Name, "Location name", MaxNameLength),
            () => OptionalSlug(request.Slug),
            () => MaxLength(request.Phone, "Phone", MaxPhoneLength),
            () => Recipients(request.Recipients));
    }

    // ---------------------------------------------------------------------------
    // Rule helpers
    // ---------------------------------------------------------------------------

    private static ValidationResult FirstFailure(params Func<ValidationResult>[] rules)
    {
        foreach (var rule in rules)
        {
            var result = rule();
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Success;
    }

    private static ValidationResult RequiredText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Failure($"{field} is required.");
        }

        if (value.Trim().Length > maxLength)
        {
            return ValidationResult.Failure($"{field} must not exceed {maxLength} characters.");
        }

        if (value.Contains('<') || value.Contains('>'))
        {
            return ValidationResult.Failure($"{field} must not contain '<' or '>'.");
        }

        return ValidationResult.Success;
    }

    private static ValidationResult MaxLength(string? value, string field, int maxLength) =>
        value is not null && value.Trim().Length > maxLength
            ? ValidationResult.Failure($"{field} must not exceed {maxLength} characters.")
            : ValidationResult.Success;

    private static ValidationResult RequiredEmail(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? ValidationResult.Failure($"{field} is required.")
            : Email(value.Trim(), field);

    private static ValidationResult OptionalEmail(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? ValidationResult.Success : Email(value.Trim(), field);

    private static ValidationResult Email(string value, string field)
    {
        if (value.Length > MaxEmailLength)
        {
            return ValidationResult.Failure($"{field} must not exceed {MaxEmailLength} characters.");
        }

        return value.Any(char.IsWhiteSpace) || !EmailValidator.IsValid(value)
            ? ValidationResult.Failure($"{field} '{value}' is not a valid email address.")
            : ValidationResult.Success;
    }

    private static ValidationResult OptionalSlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug) ? ValidationResult.Success : SlugValidator.Validate(slug.Trim());

    private static ValidationResult ProvisionableRole(string? role) =>
        role is not null && ProvisionableRoles.Contains(role, StringComparer.Ordinal)
            ? ValidationResult.Success
            : ValidationResult.Failure($"Role must be one of: {string.Join(", ", ProvisionableRoles)}.");

    private static ValidationResult LocationIds(IReadOnlyCollection<string>? locationIds)
    {
        if (locationIds is null || !locationIds.Any(id => !string.IsNullOrWhiteSpace(id)))
        {
            return ValidationResult.Failure("Location-scoped roles need at least one location.");
        }

        if (locationIds.Count > MaxLocationIds)
        {
            return ValidationResult.Failure($"A user may be given at most {MaxLocationIds} locations.");
        }

        return locationIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > MaxLocationIdLength)
            ? ValidationResult.Failure("Location ids must not be blank or longer than 100 characters.")
            : ValidationResult.Success;
    }

    private static ValidationResult Recipients(IReadOnlyCollection<string>? recipients)
    {
        if (recipients is null || recipients.Count == 0)
        {
            return ValidationResult.Failure("A location needs at least one packet recipient.");
        }

        if (recipients.Count > PacketConfigEmbedded.MaxRecipients)
        {
            return ValidationResult.Failure(
                $"A location may have at most {PacketConfigEmbedded.MaxRecipients} packet recipients.");
        }

        foreach (var recipient in recipients)
        {
            var result = RequiredEmail(recipient, "Packet recipient");
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Success;
    }

    private static string? Canonical(IReadOnlyList<string> allowed, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
