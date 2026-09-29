using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

/// <summary>
/// Tests for <see cref="ServiceRequestCreateRequestDto.AllIssues"/> — the first problem rides on
/// the request's own issue fields, so a single-problem client sends exactly what it always has,
/// and further problems follow in <see cref="ServiceRequestCreateRequestDto.AdditionalIssues"/>
/// (<c>Spec A-17</c>, issue #806).
/// </summary>
public class ServiceRequestCreateRequestDtoTests
{
    [Fact]
    public void AllIssues_WithoutAdditionalIssues_ShouldReturnTheLeadIssueOnly()
    {
        var request = BuildRequest() with
        {
            IssueDescriptionVerbatim = "slide wont come in",
            Urgency = "Soon",
            CapabilityMismatchNote = "note",
            ExpectedAttachmentCount = 2,
            DiagnosticResponses = [new DiagnosticResponseDto { QuestionText = "Does it hum?" }],
        };

        var issues = request.AllIssues();

        issues.Should().ContainSingle();
        var lead = issues[0];
        lead.IssueCategory.Should().Be("Slides");
        lead.IssueDescription.Should().Be("Slide will not retract");
        lead.IssueDescriptionVerbatim.Should().Be("slide wont come in");
        lead.Urgency.Should().Be("Soon");
        lead.CapabilityMismatchNote.Should().Be("note");
        lead.ExpectedAttachmentCount.Should().Be(2);
        lead.DiagnosticResponses.Should().ContainSingle(d => d.QuestionText == "Does it hum?");
    }

    [Fact]
    public void AllIssues_WithAdditionalIssues_ShouldKeepSubmissionOrder()
    {
        var request = BuildRequest() with
        {
            AdditionalIssues =
            [
                new IntakeIssueDto { IssueCategory = "Appliances", IssueDescription = "Fridge warm" },
                new IntakeIssueDto { IssueCategory = "Awning", IssueDescription = "Awning torn" },
            ],
        };

        var issues = request.AllIssues();

        issues.Select(i => i.IssueDescription).Should().Equal("Slide will not retract", "Fridge warm", "Awning torn");
    }

    [Fact]
    public void AllIssues_WithEmptyAdditionalIssues_ShouldReturnTheLeadIssueOnly()
    {
        var request = BuildRequest() with { AdditionalIssues = [] };

        request.AllIssues().Should().ContainSingle();
    }

    private static ServiceRequestCreateRequestDto BuildRequest() => new()
    {
        Customer = new CustomerInfoDto { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" },
        Asset = new AssetInfoDto { AssetId = "" },
        IssueCategory = "Slides",
        IssueDescription = "Slide will not retract",
    };
}
