using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Provisioning;
using RVS.Domain.Validation;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between the platform-admin provisioning DTOs and <see cref="Tenant"/> / provisioning
/// results (issue #563). Intake URLs are composed from the caller-supplied base URL.
/// </summary>
public static class TenantProvisioningMapper
{
    /// <summary>Commercial status every newly provisioned tenant starts in.</summary>
    public const string InitialStatus = "Pilot";

    /// <summary>
    /// Maps a <see cref="TenantCreateRequestDto"/> to a new <see cref="Tenant"/> in <see cref="InitialStatus"/>.
    /// </summary>
    public static Tenant ToEntity(this TenantCreateRequestDto dto, string tenantId, string? createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return new Tenant
        {
            Id = tenantId,
            Name = dto.Name.Trim(),
            BillingEmail = TrimToNull(dto.BillingEmail),
            Status = InitialStatus,
            Plan = TenantProvisioningValidator.CanonicalPlan(dto.Plan) ?? dto.Plan.Trim(),
            Notes = TrimToNull(dto.Notes),
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Applies the non-null fields of a <see cref="TenantUpdateRequestDto"/> to a tenant, mutating in
    /// place. A blank billing email or notes value clears the field.
    /// </summary>
    public static void ApplyUpdate(this Tenant entity, TenantUpdateRequestDto dto, string? updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Status is not null)
        {
            entity.Status = TenantProvisioningValidator.CanonicalStatus(dto.Status) ?? dto.Status.Trim();
        }

        if (dto.Plan is not null)
        {
            entity.Plan = TenantProvisioningValidator.CanonicalPlan(dto.Plan) ?? dto.Plan.Trim();
        }

        if (dto.BillingEmail is not null)
        {
            entity.BillingEmail = TrimToNull(dto.BillingEmail);
        }

        if (dto.Notes is not null)
        {
            entity.Notes = TrimToNull(dto.Notes);
        }

        entity.MarkAsUpdated(updatedByUserId);
    }

    /// <summary>Maps a <see cref="TenantOverview"/> to a tenant-list row.</summary>
    public static TenantSummaryResponseDto ToSummaryDto(this TenantOverview overview, string intakeBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(overview);

        var tenant = overview.Tenant;
        var gate = overview.AccessGate;

        return new TenantSummaryResponseDto
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            BillingEmail = tenant.BillingEmail,
            Status = tenant.Status,
            Plan = tenant.Plan,
            Notes = tenant.Notes,
            LoginsEnabled = gate.LoginsEnabled,
            DisabledReason = gate.DisabledReason,
            DisabledAtUtc = gate.DisabledAtUtc,
            CreatedAtUtc = tenant.CreatedAtUtc,
            UpdatedAtUtc = tenant.UpdatedAtUtc,
            Locations = [.. overview.Locations.Select(l => new TenantLocationSummaryDto
            {
                LocationId = l.Id,
                Name = l.Name,
                Slug = l.Slug,
                IntakeUrl = IntakeUrl(intakeBaseUrl, l.Slug)
            })]
        };
    }

    /// <summary>Maps a create-tenant result to its response.</summary>
    public static TenantProvisioningResponseDto ToResponseDto(this TenantProvisioningResult result, string intakeBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new TenantProvisioningResponseDto
        {
            TenantId = result.TenantId,
            Succeeded = result.Succeeded,
            Steps = [.. result.Steps.Select(s => new ProvisioningStepDto
            {
                Step = s.Name,
                Status = s.Status,
                Message = s.Message
            })],
            LocationId = result.Location?.Id,
            LocationSlug = result.Location?.Slug,
            IntakeUrl = result.Location is null ? null : IntakeUrl(intakeBaseUrl, result.Location.Slug),
            UserId = result.UserId,
            PasswordTicketUrl = result.PasswordTicket?.Url,
            PasswordTicketExpiresAtUtc = result.PasswordTicket?.ExpiresAtUtc
        };
    }

    /// <summary>Maps an add-user result to its response.</summary>
    public static TenantUserProvisioningResponseDto ToResponseDto(this TenantUserProvisioningResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new TenantUserProvisioningResponseDto
        {
            TenantId = result.TenantId,
            UserId = result.UserId,
            Email = result.Email,
            Role = result.Role,
            Status = result.Created ? ProvisioningStepStatus.Created : ProvisioningStepStatus.AlreadyExisted,
            PasswordTicketUrl = result.PasswordTicket.Url,
            PasswordTicketExpiresAtUtc = result.PasswordTicket.ExpiresAtUtc
        };
    }

    /// <summary>Maps a re-issued set-password ticket to its response.</summary>
    public static PasswordTicketResponseDto ToResponseDto(this PasswordTicket ticket, string userId)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new PasswordTicketResponseDto
        {
            UserId = userId,
            PasswordTicketUrl = ticket.Url,
            ExpiresAtUtc = ticket.ExpiresAtUtc
        };
    }

    /// <summary>Maps a location added by the admin tool to its response.</summary>
    public static TenantLocationProvisioningResponseDto ToProvisioningResponseDto(this Location location, string intakeBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(location);

        return new TenantLocationProvisioningResponseDto
        {
            TenantId = location.TenantId,
            LocationId = location.Id,
            Name = location.Name,
            Slug = location.Slug,
            IntakeUrl = IntakeUrl(intakeBaseUrl, location.Slug)
        };
    }

    private static string IntakeUrl(string baseUrl, string slug) => $"{baseUrl.TrimEnd('/')}/{slug}";

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
