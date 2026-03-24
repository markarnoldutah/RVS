using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Tests.Mappers;

public class ServiceRequestMapperTests
{
    // ── ToDetailDto ──────────────────────────────────────────────────────────

    [Fact]
    public void ToDetailDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        ServiceRequest? entity = null;

        var act = () => entity!.ToDetailDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDetailDto_WhenEntityIsValid_ShouldMapAllScalarFields()
    {
        var now = DateTime.UtcNow;
        var entity = new ServiceRequest
        {
            Id = "sr_abc123",
            TenantId = "ten_1",
            LocationId = "loc_1",
            CustomerProfileId = "cp_1",
            Status = "InProgress",
            IssueCategory = "Electrical",
            IssueDescription = "Generator not starting",
            TechnicianSummary = "Check fuel line",
            Urgency = "Today",
            RvUsage = "Full-time",
            Priority = "High",
            AssignedTechnicianId = "tech_1",
            AssignedBayId = "bay_2",
            ScheduledDateUtc = now,
            RequiredSkills = ["electrical"],
            CreatedAtUtc = now
        };

        var dto = entity.ToDetailDto();

        dto.Id.Should().Be("sr_abc123");
        dto.TenantId.Should().Be("ten_1");
        dto.LocationId.Should().Be("loc_1");
        dto.CustomerProfileId.Should().Be("cp_1");
        dto.Status.Should().Be("InProgress");
        dto.IssueCategory.Should().Be("Electrical");
        dto.IssueDescription.Should().Be("Generator not starting");
        dto.TechnicianSummary.Should().Be("Check fuel line");
        dto.Urgency.Should().Be("Today");
        dto.RvUsage.Should().Be("Full-time");
        dto.Priority.Should().Be("High");
        dto.AssignedTechnicianId.Should().Be("tech_1");
        dto.AssignedBayId.Should().Be("bay_2");
        dto.ScheduledDateUtc.Should().Be(now);
        dto.RequiredSkills.Should().ContainSingle().Which.Should().Be("electrical");
        dto.CreatedAtUtc.Should().Be(now);
    }

    [Fact]
    public void ToDetailDto_ShouldMapCustomerSnapshotToCustomerInfoDto()
    {
        var entity = new ServiceRequest
        {
            CustomerSnapshot = new CustomerSnapshotEmbedded
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@example.com",
                Phone = "(801) 555-1234"
            }
        };

        var dto = entity.ToDetailDto();

        dto.Customer.FirstName.Should().Be("Jane");
        dto.Customer.LastName.Should().Be("Doe");
        dto.Customer.Email.Should().Be("jane@example.com");
        dto.Customer.Phone.Should().Be("(801) 555-1234");
    }

    [Fact]
    public void ToDetailDto_ShouldMapAssetInfoToAssetInfoDto()
    {
        var entity = new ServiceRequest
        {
            AssetInfo = new AssetInfoEmbedded
            {
                AssetId = "RV:1HGBH41JXMN109186",
                Manufacturer = "Grand Design",
                Model = "Momentum 395G",
                Year = 2023
            }
        };

        var dto = entity.ToDetailDto();

        dto.Asset.AssetId.Should().Be("RV:1HGBH41JXMN109186");
        dto.Asset.Manufacturer.Should().Be("Grand Design");
        dto.Asset.Model.Should().Be("Momentum 395G");
        dto.Asset.Year.Should().Be(2023);
    }

    [Fact]
    public void ToDetailDto_ShouldMapAttachmentsCollection()
    {
        var createdAt = DateTime.UtcNow;
        var entity = new ServiceRequest
        {
            Attachments =
            [
                new ServiceRequestAttachmentEmbedded
                {
                    AttachmentId = "att_1",
                    FileName = "photo.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 204800,
                    BlobUri = "https://blob.example.com/photo.jpg",
                    CreatedAtUtc = createdAt
                }
            ]
        };

        var dto = entity.ToDetailDto();

        dto.Attachments.Should().ContainSingle();
        dto.Attachments[0].AttachmentId.Should().Be("att_1");
        dto.Attachments[0].FileName.Should().Be("photo.jpg");
        dto.Attachments[0].SizeBytes.Should().Be(204800);
    }

    [Fact]
    public void ToDetailDto_ShouldMapDiagnosticResponsesCollection()
    {
        var entity = new ServiceRequest
        {
            DiagnosticResponses =
            [
                new DiagnosticResponseEmbedded
                {
                    QuestionText = "Does the generator click?",
                    SelectedOptions = ["Yes"],
                    FreeTextResponse = "Clicks once"
                }
            ]
        };

        var dto = entity.ToDetailDto();

        dto.DiagnosticResponses.Should().ContainSingle();
        dto.DiagnosticResponses[0].QuestionText.Should().Be("Does the generator click?");
        dto.DiagnosticResponses[0].SelectedOptions.Should().ContainSingle().Which.Should().Be("Yes");
        dto.DiagnosticResponses[0].FreeTextResponse.Should().Be("Clicks once");
    }

    // ── ToSummaryDto ─────────────────────────────────────────────────────────

    [Fact]
    public void ToSummaryDto_WhenEntityIsNull_ShouldThrowArgumentNullException()
    {
        ServiceRequest? entity = null;

        var act = () => entity!.ToSummaryDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToSummaryDto_ShouldMapCoreFields()
    {
        var now = DateTime.UtcNow;
        var entity = new ServiceRequest
        {
            Id = "sr_001",
            LocationId = "loc_1",
            Status = "New",
            IssueCategory = "Slide System",
            Priority = "High",
            AssignedTechnicianId = "tech_2",
            TechnicianSummary = "Slide motor failed",
            CreatedAtUtc = now,
            CustomerSnapshot = new CustomerSnapshotEmbedded { FirstName = "Mike", LastName = "Johnson" },
            AssetInfo = new AssetInfoEmbedded { Year = 2021, Manufacturer = "Forest River", Model = "XLR" }
        };

        var dto = entity.ToSummaryDto();

        dto.Id.Should().Be("sr_001");
        dto.LocationId.Should().Be("loc_1");
        dto.Status.Should().Be("New");
        dto.CustomerFullName.Should().Be("Mike Johnson");
        dto.AssetDisplay.Should().Be("2021 Forest River XLR");
        dto.IssueCategory.Should().Be("Slide System");
        dto.TechnicianSummary.Should().Be("Slide motor failed");
        dto.Priority.Should().Be("High");
        dto.AssignedTechnicianId.Should().Be("tech_2");
        dto.AttachmentCount.Should().Be(0);
        dto.CreatedAtUtc.Should().Be(now);
    }

    [Fact]
    public void ToSummaryDto_WhenAssetInfoIsEmpty_AssetDisplayShouldBeNull()
    {
        var entity = new ServiceRequest
        {
            AssetInfo = new AssetInfoEmbedded { AssetId = "RV:1HGBH41JXMN109186" }
        };

        var dto = entity.ToSummaryDto();

        dto.AssetDisplay.Should().BeNull();
    }

    // ── ToEntity (create) ────────────────────────────────────────────────────

    [Fact]
    public void ToEntity_WhenDtoIsNull_ShouldThrowArgumentNullException()
    {
        ServiceRequestCreateRequestDto? dto = null;

        var act = () => dto!.ToEntity("ten_1", "usr_1");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToEntity_WhenTenantIdIsEmpty_ShouldThrowArgumentException()
    {
        var dto = BuildValidCreateRequest();

        var act = () => dto.ToEntity("", "usr_1");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEntity_WhenCreatedByUserIdIsEmpty_ShouldThrowArgumentException()
    {
        var dto = BuildValidCreateRequest();

        var act = () => dto.ToEntity("ten_1", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEntity_ShouldMapAllFields()
    {
        var dto = BuildValidCreateRequest();

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.Id.Should().StartWith("sr_");
        entity.TenantId.Should().Be("ten_1");
        entity.CreatedByUserId.Should().Be("usr_1");
        entity.Status.Should().Be("New");
        entity.IssueCategory.Should().Be("Slide System");
        entity.IssueDescription.Should().Be("Slide won't retract");
        entity.Urgency.Should().Be("Today");
        entity.RvUsage.Should().Be("Full-time");
    }

    [Fact]
    public void ToEntity_ShouldMapCustomerSnapshot()
    {
        var dto = BuildValidCreateRequest();

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.CustomerSnapshot.FirstName.Should().Be("Jane");
        entity.CustomerSnapshot.LastName.Should().Be("Doe");
        entity.CustomerSnapshot.Email.Should().Be("jane@example.com");
        entity.CustomerSnapshot.Phone.Should().Be("(801) 555-1234");
    }

    [Fact]
    public void ToEntity_ShouldMapAssetInfo()
    {
        var dto = BuildValidCreateRequest();

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.AssetInfo.AssetId.Should().Be("RV:1HGBH41JXMN109186");
        entity.AssetInfo.Manufacturer.Should().Be("Grand Design");
        entity.AssetInfo.Model.Should().Be("Momentum 395G");
        entity.AssetInfo.Year.Should().Be(2023);
    }

    [Fact]
    public void ToEntity_ShouldTrimStringFields()
    {
        var dto = new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto { FirstName = "  Jane  ", LastName = " Doe ", Email = " jane@example.com " },
            Asset = new AssetInfoDto { AssetId = " RV:1HGBH41JXMN109186 " },
            IssueCategory = "  Slide System  ",
            IssueDescription = "  Slide won't retract  "
        };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.IssueCategory.Should().Be("Slide System");
        entity.IssueDescription.Should().Be("Slide won't retract");
        entity.CustomerSnapshot.FirstName.Should().Be("Jane");
        entity.CustomerSnapshot.LastName.Should().Be("Doe");
        entity.CustomerSnapshot.Email.Should().Be("jane@example.com");
        entity.AssetInfo.AssetId.Should().Be("RV:1HGBH41JXMN109186");
    }

    [Fact]
    public void ToEntity_WhenNoDiagnosticResponses_ShouldHaveEmptyList()
    {
        var dto = BuildValidCreateRequest() with { DiagnosticResponses = null };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.DiagnosticResponses.Should().BeEmpty();
    }

    [Fact]
    public void ToEntity_ShouldMapDiagnosticResponses()
    {
        var dto = BuildValidCreateRequest() with
        {
            DiagnosticResponses =
            [
                new DiagnosticResponseDto
                {
                    QuestionText = "Does it click?",
                    SelectedOptions = ["Yes"],
                    FreeTextResponse = "Once"
                }
            ]
        };

        var entity = dto.ToEntity("ten_1", "usr_1");

        entity.DiagnosticResponses.Should().ContainSingle();
        entity.DiagnosticResponses[0].QuestionText.Should().Be("Does it click?");
    }

    // ── ToSummaryPagedResult ─────────────────────────────────────────────────

    [Fact]
    public void ToSummaryPagedResult_WhenPagedResultIsNull_ShouldThrowArgumentNullException()
    {
        PagedResult<ServiceRequest>? paged = null;

        var act = () => paged!.ToSummaryPagedResult();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToSummaryPagedResult_ShouldCarryForwardPaginationFields()
    {
        var paged = new PagedResult<ServiceRequest>
        {
            Page = 2,
            PageSize = 10,
            TotalCount = 45,
            ContinuationToken = "tok_abc",
            Items = [new ServiceRequest()]
        };

        var result = paged.ToSummaryPagedResult();

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(45);
        result.ContinuationToken.Should().Be("tok_abc");
        result.Items.Should().ContainSingle();
    }

    // ── Embedded helpers ─────────────────────────────────────────────────────

    [Fact]
    public void CustomerSnapshotToDto_WhenNull_ShouldThrowArgumentNullException()
    {
        CustomerSnapshotEmbedded? snapshot = null;

        var act = () => snapshot!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AssetInfoToDto_WhenNull_ShouldThrowArgumentNullException()
    {
        AssetInfoEmbedded? asset = null;

        var act = () => asset!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AttachmentToDto_WhenNull_ShouldThrowArgumentNullException()
    {
        ServiceRequestAttachmentEmbedded? attachment = null;

        var act = () => attachment!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DiagnosticResponseToDto_WhenNull_ShouldThrowArgumentNullException()
    {
        DiagnosticResponseEmbedded? response = null;

        var act = () => response!.ToDto();

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ServiceRequestCreateRequestDto BuildValidCreateRequest() =>
        new()
        {
            Customer = new CustomerInfoDto
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@example.com",
                Phone = "(801) 555-1234"
            },
            Asset = new AssetInfoDto
            {
                AssetId = "RV:1HGBH41JXMN109186",
                Manufacturer = "Grand Design",
                Model = "Momentum 395G",
                Year = 2023
            },
            IssueCategory = "Slide System",
            IssueDescription = "Slide won't retract",
            Urgency = "Today",
            RvUsage = "Full-time"
        };
}
