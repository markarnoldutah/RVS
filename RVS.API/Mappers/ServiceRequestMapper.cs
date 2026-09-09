using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

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
            HasExtendedWarranty = entity.HasExtendedWarranty,
            ApproxPurchaseDate = entity.ApproxPurchaseDate,
            Priority = entity.Priority,
            AssignedTechnicianId = entity.AssignedTechnicianId,
            AssignedBayId = entity.AssignedBayId,
            ScheduledDateUtc = entity.ScheduledDateUtc,
            RequiredSkills = entity.RequiredSkills,
            BoardSequence = entity.BoardSequence,
            DiagnosticResponses = entity.DiagnosticResponses.Select(d => d.ToDto()).ToList(),
            Attachments = entity.Attachments.Select(a => a.ToDto()).ToList(),
            AiEnrichment = entity.AiEnrichment?.ToDto(),
            CustomerStatusNote = entity.CustomerStatusNote is { } note
                ? new CustomerStatusNoteDto { Text = note.Text, UpdatedAtUtc = note.UpdatedAtUtc }
                : null,
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

        var hasOutcome = entity.ServiceEvent is not null
            && (!string.IsNullOrWhiteSpace(entity.ServiceEvent.FailureMode)
                || !string.IsNullOrWhiteSpace(entity.ServiceEvent.RepairAction));

        return new ServiceRequestSummaryResponseDto
        {
            Id = entity.Id,
            LocationId = entity.LocationId,
            Status = entity.Status,
            CustomerFullName = $"{entity.CustomerSnapshot.FirstName} {entity.CustomerSnapshot.LastName}".Trim(),
            AssetDisplay = ComposeAssetDisplay(entity.AssetInfo),
            IssueCategory = entity.IssueCategory ?? string.Empty,
            TechnicianSummary = entity.TechnicianSummary,
            AttachmentCount = entity.Attachments.Count,
            AssignedTechnicianId = entity.AssignedTechnicianId,
            Priority = entity.Priority,
            BoardSequence = entity.BoardSequence,
            HasOutcome = hasOutcome,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Maps a <see cref="ServiceRequest"/> to the customer-facing status view
    /// (<c>Spec X-1</c> / <c>C-9</c>): the unit, the submission date, the current status, the
    /// servicing location's phone number, and any manager-authored status note. No customer
    /// identity and no free-text problem description are carried across this boundary.
    /// </summary>
    /// <param name="entity">The service request.</param>
    /// <param name="locationPhone">Phone number of the servicing location, if known.</param>
    public static CustomerStatusItemResponseDto ToCustomerStatusItemDto(this ServiceRequest entity, string? locationPhone)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CustomerStatusItemResponseDto
        {
            Unit = ComposeAssetDisplay(entity.AssetInfo),
            SubmittedAtUtc = entity.CreatedAtUtc,
            Status = entity.Status,
            LocationPhone = string.IsNullOrWhiteSpace(locationPhone) ? null : locationPhone.Trim(),
            StatusNote = string.IsNullOrWhiteSpace(entity.CustomerStatusNote?.Text)
                ? null
                : entity.CustomerStatusNote.Text
        };
    }

    /// <summary>
    /// Builds the "year make model" display string for a unit, or <c>null</c> when none
    /// of those fields are populated.
    /// </summary>
    private static string? ComposeAssetDisplay(AssetInfoEmbedded assetInfo)
    {
        var year = assetInfo.Year.HasValue ? $"{assetInfo.Year} " : string.Empty;
        var manufacturer = assetInfo.Manufacturer is not null ? $"{assetInfo.Manufacturer} " : string.Empty;
        var model = assetInfo.Model ?? string.Empty;
        var display = (year + manufacturer + model).Trim();

        return string.IsNullOrWhiteSpace(display) ? null : display;
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
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Status = "New",
            IssueCategory = dto.IssueCategory.Trim(),
            IssueDescription = dto.IssueDescription.Trim(),
            Urgency = dto.Urgency?.Trim(),
            RvUsage = dto.RvUsage?.Trim(),
            HasExtendedWarranty = dto.HasExtendedWarranty?.Trim(),
            ApproxPurchaseDate = dto.ApproxPurchaseDate?.Trim(),
            CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = dto.Customer.FirstName.Trim(),
                LastName = dto.Customer.LastName.Trim(),
                Email = dto.Customer.Email.Trim(),
                Phone = dto.Customer.Phone?.Trim(),
                PreferredContact = PreferredContactMethod.Normalize(dto.Customer.PreferredContact)
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
        entity.HasExtendedWarranty = dto.HasExtendedWarranty?.Trim();
        entity.ApproxPurchaseDate = dto.ApproxPurchaseDate?.Trim();
        entity.AssignedTechnicianId = dto.AssignedTechnicianId?.Trim();
        entity.AssignedBayId = dto.AssignedBayId?.Trim();
        entity.ScheduledDateUtc = dto.ScheduledDateUtc;
        entity.RequiredSkills = dto.RequiredSkills;
        entity.BoardSequence = dto.BoardSequence ?? entity.BoardSequence;
        entity.ServiceEvent = dto.ServiceEvent?.ToEmbedded();

        if (dto.Customer is not null)
        {
            entity.CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = dto.Customer.FirstName.Trim(),
                LastName = dto.Customer.LastName.Trim(),
                Email = dto.Customer.Email.Trim(),
                Phone = dto.Customer.Phone?.Trim(),
                PreferredContact = PreferredContactMethod.Normalize(dto.Customer.PreferredContact)
            };
        }

        if (dto.Asset is not null)
        {
            entity.AssetInfo = new AssetInfoEmbedded
            {
                AssetId = dto.Asset.AssetId.Trim(),
                Manufacturer = dto.Asset.Manufacturer?.Trim(),
                Model = dto.Asset.Model?.Trim(),
                Year = dto.Asset.Year
            };
        }

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
            Phone = snapshot.Phone,
            PreferredContact = snapshot.PreferredContact
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

    /// <summary>
    /// Maps an <see cref="AiEnrichmentMetadataEmbedded"/> to an <see cref="AiEnrichmentMetadataDto"/>.
    /// </summary>
    public static AiEnrichmentMetadataDto ToDto(this AiEnrichmentMetadataEmbedded entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new AiEnrichmentMetadataDto
        {
            CategorySuggestionProvider = entity.CategorySuggestionProvider,
            CategorySuggestionConfidence = entity.CategorySuggestionConfidence,
            DiagnosticQuestionsProvider = entity.DiagnosticQuestionsProvider,
            TranscriptionProvider = entity.TranscriptionProvider,
            TranscriptionConfidence = entity.TranscriptionConfidence,
            VinExtractionProvider = entity.VinExtractionProvider,
            VinExtractionConfidence = entity.VinExtractionConfidence,
            InsightsSuggestionProvider = entity.InsightsSuggestionProvider,
            InsightsSuggestionConfidence = entity.InsightsSuggestionConfidence,
            EnrichedAtUtc = entity.EnrichedAtUtc
        };
    }
}
