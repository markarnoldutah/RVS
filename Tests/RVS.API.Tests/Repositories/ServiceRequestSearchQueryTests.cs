using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Validation;
using RVS.Infra.AzCosmosRepository.Repositories;

namespace RVS.API.Tests.Repositories;

/// <summary>
/// Tests for <see cref="ServiceRequestSearchQuery"/> — the Cosmos SQL behind the service-request
/// search (issue #849).
///
/// Contract under test: the board scope returns every open request plus closed ones changed inside
/// the Done window, with no row cap; the list scope returns the newest matches up to
/// <see cref="ServiceRequestSearch.MaxListResults"/> and reports when it was cut; only the
/// set-narrowing filters reach the server; and the projection carries everything the summary needs.
/// </summary>
public class ServiceRequestSearchQueryTests
{
    private const string TenantId = "ten_1";
    private static readonly DateTime NowUtc = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    // ── Build: always ────────────────────────────────────────────────────────

    [Fact]
    public void Build_Always_ScopesToTenantAndServiceRequestType()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto(), NowUtc);

        WhereClause(query.QueryText).Should().Contain("c.tenantId = @tenantId").And.Contain("c.type = 'serviceRequest'");
        Parameter(query, "@tenantId").Should().Be(TenantId);
    }

    [Fact]
    public void Build_Always_OrdersNewestFirst()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto(), NowUtc);

        query.QueryText.Should().EndWith("ORDER BY c.createdAtUtc DESC");
    }

    [Fact]
    public void Build_Always_ProjectsSummaryFieldsRatherThanWholeDocuments()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto(), NowUtc);

        var select = SelectClause(query.QueryText);
        select.Should().NotContain("*");
        select.Should().NotContain("issueDescription");
        select.Should().NotContain("packetGeneration");
        foreach (var field in ServiceRequestSearchQuery.SummaryFields)
        {
            select.Should().Contain($"c.{field}");
        }
    }

    // ── Build: scope ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("List")]
    public void Build_ListScope_ReadsOneRowPastTheCap(string? scope)
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Scope = scope }, NowUtc);

        query.QueryText.Should().StartWith($"SELECT TOP {ServiceRequestSearch.MaxListResults + 1} ");
        query.QueryText.Should().NotContain("@doneCutoff");
    }

    [Fact]
    public void Build_BoardScope_HasNoRowCap()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Scope = "Board" }, NowUtc);

        query.QueryText.Should().NotContain("TOP");
    }

    [Fact]
    public void Build_BoardScope_KeepsOpenWorkAndClosedWorkChangedInsideTheDoneWindow()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Scope = "Board" }, NowUtc);

        var where = WhereClause(query.QueryText);
        where.Should().Contain("c.status NOT IN ('Completed', 'Cancelled')");
        where.Should().Contain("c.updatedAtUtc > @doneCutoff");
        Parameter(query, "@doneCutoff").Should().Be(BoardRetentionFilter.DoneColumnCutoffUtc(NowUtc));
    }

    [Fact]
    public void Build_BoardScope_FallsBackToCreatedDateWhenNeverUpdated()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Scope = "Board" }, NowUtc);

        WhereClause(query.QueryText).Should()
            .Contain("((NOT IS_DEFINED(c.updatedAtUtc) OR IS_NULL(c.updatedAtUtc)) AND c.createdAtUtc > @doneCutoff)");
    }

    // ── Build: filters ───────────────────────────────────────────────────────

    [Fact]
    public void Build_WhenStatusIsOpen_ExcludesClosedStatusesWithoutAStatusParameter()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Status = "Open" }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("c.status NOT IN ('Completed', 'Cancelled')");
        query.GetQueryParameters().Select(p => p.Name).Should().NotContain("@status");
    }

    [Fact]
    public void Build_WhenStatusIsSpecific_MatchesItExactly()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Status = "Completed" }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("c.status = @status");
        Parameter(query, "@status").Should().Be("Completed");
    }

    [Fact]
    public void Build_WhenLocationAndDatesAreSet_FiltersOnThem()
    {
        var from = new DateTime(2025, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var request = new ServiceRequestSearchRequestDto { LocationId = "loc_1", DateFrom = from, DateTo = to };

        var query = ServiceRequestSearchQuery.Build(TenantId, request, NowUtc);

        var where = WhereClause(query.QueryText);
        where.Should().Contain("c.locationId = @locationId")
            .And.Contain("c.createdAtUtc >= @dateFrom")
            .And.Contain("c.createdAtUtc <= @dateTo");
        Parameter(query, "@locationId").Should().Be("loc_1");
        Parameter(query, "@dateFrom").Should().Be(from);
        Parameter(query, "@dateTo").Should().Be(to);
    }

    [Fact]
    public void Build_WhenKeywordIsSet_MatchesNameIssueTextAndVin()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Keyword = "1HGBH" }, NowUtc);

        var where = WhereClause(query.QueryText);
        where.Should().Contain("c.customerSnapshot.lastName")
            .And.Contain("c.issueDescription")
            .And.Contain("c.assetInfo.assetId");
        Parameter(query, "@keyword").Should().Be("1HGBH");
    }

    [Fact]
    public void Build_WhenJobTypeIsNotTriaged_MatchesUnsetJobTypes()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { JobType = JobTypes.NotTriagedFilter }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("(NOT IS_DEFINED(c.jobType) OR IS_NULL(c.jobType))");
        query.GetQueryParameters().Select(p => p.Name).Should().NotContain("@jobType");
    }

    [Fact]
    public void Build_WhenJobTypeIsACode_MatchesItExactly()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { JobType = JobTypes.Remote }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("c.jobType = @jobType");
        Parameter(query, "@jobType").Should().Be(JobTypes.Remote);
    }

    [Fact]
    public void Build_WhenCategoryIsSet_MatchesItsCanonicalCode()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { IssueCategory = " hvac " }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("c.issueCategory = @issueCategory");
        Parameter(query, "@issueCategory").Should().Be("HVAC");
    }

    [Fact]
    public void Build_WhenPriorityIsSet_MatchesItExactly()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { Priority = "Critical" }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("c.priority = @priority");
        Parameter(query, "@priority").Should().Be("Critical");
    }

    [Fact]
    public void Build_WhenTechnicianIsSet_MatchesPartialTextIgnoringCase()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto { AssignedTechnicianId = " Maria " }, NowUtc);

        WhereClause(query.QueryText).Should().Contain("CONTAINS(LOWER(c.assignedTechnicianId), LOWER(@assignedTechnicianId))");
        Parameter(query, "@assignedTechnicianId").Should().Be("Maria");
    }

    [Fact]
    public void Build_WhenEveryFilterIsSet_RequiresAllOfThem()
    {
        var request = new ServiceRequestSearchRequestDto
        {
            Keyword = "slide", Status = "Open", LocationId = "loc_1", JobType = JobTypes.OnSite,
            IssueCategory = "Slides", Priority = "High", AssignedTechnicianId = "tech_1",
            DateFrom = NowUtc.AddYears(-3), DateTo = NowUtc,
        };

        var query = ServiceRequestSearchQuery.Build(TenantId, request, NowUtc);

        // tenant, type, status, location, from, to, job type, keyword, category, priority, technician
        WhereClause(query.QueryText).Split(" AND ").Should().HaveCount(11);
    }

    [Fact]
    public void Build_WithNoFilters_FiltersOnlyOnTenantAndType()
    {
        var query = ServiceRequestSearchQuery.Build(TenantId, new ServiceRequestSearchRequestDto(), NowUtc);

        WhereClause(query.QueryText).Should().Be("c.tenantId = @tenantId AND c.type = 'serviceRequest'");
        query.GetQueryParameters().Should().ContainSingle();
    }

    [Fact]
    public void Build_WhenTenantIdIsBlank_ShouldThrowArgumentException()
    {
        var act = () => ServiceRequestSearchQuery.Build(" ", new ServiceRequestSearchRequestDto(), NowUtc);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => ServiceRequestSearchQuery.Build(TenantId, null!, NowUtc);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Projection ───────────────────────────────────────────────────────────

    [Fact]
    public void SummaryFields_WhenDocumentIsProjected_ShouldYieldTheSameSummary()
    {
        var entity = BuildFullServiceRequest();
        var document = JObject.FromObject(entity, JsonSerializer.CreateDefault());
        var projected = new JObject(document.Properties()
            .Where(p => ServiceRequestSearchQuery.SummaryFields.Contains(p.Name)));

        var roundTripped = projected.ToObject<ServiceRequest>()!;

        projected.ContainsKey("issueDescription").Should().BeFalse("the projection exists to leave the heavy fields behind");
        roundTripped.ToSummaryDto().Should().BeEquivalentTo(entity.ToSummaryDto());
    }

    // ── ToResult ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    public void ToResult_ListScope_WhenAtOrUnderCap_ShouldReturnEveryRowUntruncated(int count)
    {
        var rows = BuildRows(count);

        var result = ServiceRequestSearchQuery.ToResult(rows, scope: null);

        result.Items.Should().HaveCount(count);
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void ToResult_ListScope_WhenOverCap_ShouldKeepTheNewestCapRowsAndFlagTruncation()
    {
        var rows = BuildRows(ServiceRequestSearch.MaxListResults + 1);

        var result = ServiceRequestSearchQuery.ToResult(rows, scope: "List");

        result.Items.Should().HaveCount(ServiceRequestSearch.MaxListResults);
        result.Items.Should().Equal(rows.Take(ServiceRequestSearch.MaxListResults));
        result.IsTruncated.Should().BeTrue();
    }

    [Fact]
    public void ToResult_BoardScope_ShouldNeverTruncate()
    {
        var rows = BuildRows(ServiceRequestSearch.MaxListResults + 100);

        var result = ServiceRequestSearchQuery.ToResult(rows, scope: "Board");

        result.Items.Should().HaveCount(rows.Count);
        result.IsTruncated.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string SelectClause(string sql) => sql[..sql.IndexOf(" FROM c", StringComparison.Ordinal)];

    private static string WhereClause(string sql)
    {
        var start = sql.IndexOf(" WHERE ", StringComparison.Ordinal) + " WHERE ".Length;
        var end = sql.IndexOf(" ORDER BY ", StringComparison.Ordinal);
        return sql[start..end];
    }

    private static object Parameter(Microsoft.Azure.Cosmos.QueryDefinition query, string name) =>
        query.GetQueryParameters().Single(p => p.Name == name).Value;

    private static List<ServiceRequest> BuildRows(int count) =>
        Enumerable.Range(0, count).Select(i => new ServiceRequest { Id = $"sr_{i}", TenantId = TenantId }).ToList();

    private static ServiceRequest BuildFullServiceRequest() => new()
    {
        Id = "sr_full",
        TenantId = TenantId,
        Status = "Cancelled",
        LocationId = "loc_1",
        CustomerProfileId = "cp_1",
        CustomerSnapshot = new CustomerSnapshotEmbedded { FirstName = "Jane", LastName = "Doe" },
        AssetInfo = new AssetInfoEmbedded { AssetId = "1HGBH41JXMN109186", Manufacturer = "Grand Design", Model = "Reflection", Year = 2022 },
        IssueDescription = "Slide won't retract",
        IssueCategory = "SLIDE",
        TechnicianSummary = "Check the slide motor",
        Attachments = [new ServiceRequestAttachmentEmbedded { FileName = "a.jpg" }, new ServiceRequestAttachmentEmbedded { FileName = "b.jpg" }],
        DiagnosticResponses = [new DiagnosticResponseEmbedded { QuestionText = "Does it click?" }],
        ScheduledStartUtc = new DateTime(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc),
        ScheduledTimeZone = "America/Denver",
        ScheduledTimeIsSet = true,
        AssignedTechnicianId = "tech_1",
        JobType = JobTypes.OnSite,
        Priority = "High",
        SubmissionId = "sr_first",
        SubmissionPosition = 2,
        SubmissionCount = 3,
        BoardSequence = 7,
        Disposition = new DispositionEmbedded { ReasonCode = DispositionReasons.Duplicate },
        CreatedAtUtc = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
    };
}
