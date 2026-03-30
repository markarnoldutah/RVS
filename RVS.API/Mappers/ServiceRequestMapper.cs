using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between <see cref="ServiceRequest"/> entities and their DTOs at the API boundary.
/// Includes helpers for embedded types: <see cref="CustomerSnapshotEmbedded"/>,
/// <see cref="AssetInfoEmbedded"/>, <see cref="ServiceRequestAttachmentEmbedded"/>,
/// and <see cref="DiagnosticResponseEmbedded"/>.
/// </summary>
public static class ServiceRequestMapper
{
    /// <summary>
    /// Maps a <see cref="ServiceRequest"/> entity to a <see cref="ServiceRequestDetailResponseDto"/>.
    /// </summary>
    public static ServiceRequestDetailResponseDto ToDetailDto(this ServiceRequest entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new ServiceRequestDetailResponseDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Status = entity.Status,
            LocationId = entity.LocationId,
            CustomerProfileId = entity.CustomerProfileId,
            Customer = entity.CustomerSnapshot.ToDto(),
            Asset = entity.AssetInfo.ToDto(),
            IssueCategory = entity.IssueCategory ?? string.Empty,
            IssueDescription = entity.IssueDescription,
            TechnicianSummary = entity.TechnicianSummary,
            Urgency = entity.Urgency,
            RvUsage = entity.RvUsage,
            Priority = entity.Priority,
            AssignedTechnicianId = entity.AssignedTechnicianId,
            AssignedBayId = entity.AssignedBayId,
            ScheduledDateUtc = entity.ScheduledDateUtc,
            RequiredSkills = entity.RequiredSkills,
            DiagnosticResponses = entity.DiagnosticResponses.Select(d => d.ToDto()).ToList(),
            Attachments = entity.Attachments.Select(a => a.ToDto()).ToList(),
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="ServiceRequest"/> entity to a <see cref="ServiceRequestSummaryResponseDto"/>.
    /// </summary>
    public static ServiceRequestSummaryResponseDto ToSummaryDto(this ServiceRequest entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var year = entity.AssetInfo.Year.HasValue ? $"{entity.AssetInfo.Year} " : string.Empty;
        var manufacturer = entity.AssetInfo.Manufacturer is not null ? $"{entity.AssetInfo.Manufacturer} " : string.Empty;
        var model = entity.AssetInfo.Model ?? string.Empty;
        var assetDisplay = (year + manufacturer + model).Trim();

        var hasOutcome = entity.ServiceEvent is not null
            && (!string.IsNullOrWhiteSpace(entity.ServiceEvent.FailureMode)
                || !string.IsNullOrWhiteSpace(entity.ServiceEvent.RepairAction));

        return new ServiceRequestSummaryResponseDto
        {
            Id = entity.Id,
            LocationId = entity.LocationId,
            Status = entity.Status,
            CustomerFullName = $"{entity.CustomerSnapshot.FirstName} {entity.CustomerSnapshot.LastName}".Trim(),
            AssetDisplay = string.IsNullOrWhiteSpace(assetDisplay) ? null : assetDisplay,
            IssueCategory = entity.IssueCategory ?? string.Empty,
            TechnicianSummary = entity.TechnicianSummary,
            AttachmentCount = entity.Attachments.Count,
            AssignedTechnicianId = entity.AssignedTechnicianId,
            Priority = entity.Priority,
            HasOutcome = hasOutcome,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="ServiceRequestCreateRequestDto"/> to a new <see cref="ServiceRequest"/> entity.
    /// </summary>
    /// <param name="dto">The create request DTO.</param>
    /// <param name="tenantId">Tenant identifier for tenant isolation.</param>
    /// <param name="createdByUserId">The ID of the user creating the request.</param>
    public static ServiceRequest ToEntity(this ServiceRequestCreateRequestDto dto, string tenantId, string createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);

        return new ServiceRequest
        {
            Id = $"sr_{Guid.NewGuid():N}",
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Status = "New",
            IssueCategory = dto.IssueCategory.Trim(),
            IssueDescription = dto.IssueDescription.Trim(),
            Urgency = dto.Urgency?.Trim(),
            RvUsage = dto.RvUsage?.Trim(),
            CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = dto.Customer.FirstName.Trim(),
                LastName = dto.Customer.LastName.Trim(),
                Email = dto.Customer.Email.Trim(),
                Phone = dto.Customer.Phone?.Trim()
            },
            AssetInfo = new AssetInfoEmbedded
            {
                AssetId = dto.Asset.AssetId.Trim(),
                Manufacturer = dto.Asset.Manufacturer?.Trim(),
                Model = dto.Asset.Model?.Trim(),
                Year = dto.Asset.Year
            },
            DiagnosticResponses = dto.DiagnosticResponses?
                .Select(d => new DiagnosticResponseEmbedded
                {
                    QuestionText = d.QuestionText.Trim(),
                    SelectedOptions = d.SelectedOptions,
                    FreeTextResponse = d.FreeTextResponse?.Trim()
                })
                .ToList() ?? []
        };
    }

    /// <summary>
    /// Applies update values from a <see cref="ServiceRequestUpdateRequestDto"/> to an existing
    /// <see cref="ServiceRequest"/> entity, mutating in place.
    /// </summary>
    /// <param name="entity">The service request entity to update.</param>
    /// <param name="dto">The request DTO containing updated values.</param>
    /// <param name="updatedByUserId">The ID of the user performing the update.</param>
    public static void ApplyUpdate(this ServiceRequest entity, ServiceRequestUpdateRequestDto dto, string? updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        entity.Status = dto.Status.Trim();
        entity.IssueDescription = dto.IssueDescription.Trim();
        entity.IssueCategory = dto.IssueCategory?.Trim();
        entity.TechnicianSummary = dto.TechnicianSummary?.Trim();
        entity.Priority = dto.Priority.Trim();
        entity.Urgency = dto.Urgency?.Trim();
        entity.RvUsage = dto.RvUsage?.Trim();
        entity.AssignedTechnicianId = dto.AssignedTechnicianId?.Trim();
        entity.AssignedBayId = dto.AssignedBayId?.Trim();
        entity.ScheduledDateUtc = dto.ScheduledDateUtc;
        entity.RequiredSkills = dto.RequiredSkills;
        entity.ServiceEvent = dto.ServiceEvent?.ToEmbedded();
        entity.MarkAsUpdated(updatedByUserId);
    }

    /// <summary>
    /// Maps a <see cref="ServiceEventDto"/> to a <see cref="ServiceEventEmbedded"/>.
    /// </summary>
    public static ServiceEventEmbedded ToEmbedded(this ServiceEventDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ServiceEventEmbedded
        {
            ComponentType = dto.ComponentType?.Trim(),
            FailureMode = dto.FailureMode?.Trim(),
            RepairAction = dto.RepairAction?.Trim(),
            PartsUsed = dto.PartsUsed,
            LaborHours = dto.LaborHours,
            ServiceDateUtc = dto.ServiceDateUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="ServiceEventEmbedded"/> to a <see cref="ServiceEventDto"/>.
    /// </summary>
    public static ServiceEventDto ToDto(this ServiceEventEmbedded entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new ServiceEventDto
        {
            ComponentType = entity.ComponentType,
            FailureMode = entity.FailureMode,
            RepairAction = entity.RepairAction,
            PartsUsed = entity.PartsUsed,
            LaborHours = entity.LaborHours,
            ServiceDateUtc = entity.ServiceDateUtc
        };
    }

    /// <summary>
    /// Maps a paged result of <see cref="ServiceRequest"/> entities to a paged result of summary DTOs.
    /// </summary>
    public static PagedResult<ServiceRequestSummaryResponseDto> ToSummaryPagedResult(
        this PagedResult<ServiceRequest> pagedResult)
    {
        ArgumentNullException.ThrowIfNull(pagedResult);

        return new PagedResult<ServiceRequestSummaryResponseDto>
        {
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            ContinuationToken = pagedResult.ContinuationToken,
            Items = pagedResult.Items.Select(e => e.ToSummaryDto()).ToList()
        };
    }

    /// <summary>
    /// Maps a <see cref="CustomerSnapshotEmbedded"/> to a <see cref="CustomerInfoDto"/>.
    /// </summary>
    public static CustomerInfoDto ToDto(this CustomerSnapshotEmbedded snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new CustomerInfoDto
        {
            FirstName = snapshot.FirstName,
            LastName = snapshot.LastName,
            Email = snapshot.Email,
            Phone = snapshot.Phone
        };
    }

    /// <summary>
    /// Maps an <see cref="AssetInfoEmbedded"/> to an <see cref="AssetInfoDto"/>.
    /// </summary>
    public static AssetInfoDto ToDto(this AssetInfoEmbedded asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return new AssetInfoDto
        {
            AssetId = asset.AssetId,
            Manufacturer = asset.Manufacturer,
            Model = asset.Model,
            Year = asset.Year
        };
    }

    /// <summary>
    /// Maps a <see cref="ServiceRequestAttachmentEmbedded"/> to an <see cref="AttachmentDto"/>.
    /// </summary>
    public static AttachmentDto ToDto(this ServiceRequestAttachmentEmbedded attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new AttachmentDto
        {
            AttachmentId = attachment.AttachmentId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            BlobUri = attachment.BlobUri,
            CreatedAtUtc = attachment.CreatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="DiagnosticResponseEmbedded"/> to a <see cref="DiagnosticResponseDto"/>.
    /// </summary>
    public static DiagnosticResponseDto ToDto(this DiagnosticResponseEmbedded response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new DiagnosticResponseDto
        {
            QuestionText = response.QuestionText,
            SelectedOptions = response.SelectedOptions,
            FreeTextResponse = response.FreeTextResponse
        };
    }
}
