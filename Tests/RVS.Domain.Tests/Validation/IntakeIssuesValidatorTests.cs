using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="IntakeIssuesValidator"/> — up to ten problems per submission, each with a
/// description intake would accept on its own (<c>Spec A-17</c>, issue #806).
/// </summary>
public class IntakeIssuesValidatorTests
{
    [Fact]
    public void MaxIssuesPerSubmission_ShouldBeTen()
    {
        IntakeIssuesValidator.MaxIssuesPerSubmission.Should().Be(10);
    }

    [Fact]
    public void Validate_NullRequest_ShouldThrowArgumentNullException()
    {
        var act = () => IntakeIssuesValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_SingleIssueWithoutAdditionalIssues_ShouldPass()
    {
        var result = IntakeIssuesValidator.Validate(BuildRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TenIssuesInTotal_ShouldPass()
    {
        var request = BuildRequest(additional: Enumerable.Range(0, 9).Select(i => Issue($"Problem {i + 2}")).ToList());

        IntakeIssuesValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ElevenIssuesInTotal_ShouldFail()
    {
        var request = BuildRequest(additional: Enumerable.Range(0, 10).Select(i => Issue($"Problem {i + 2}")).ToList());

        var result = IntakeIssuesValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("10");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankLeadDescription_ShouldFail(string description)
    {
        var result = IntakeIssuesValidator.Validate(BuildRequest(leadDescription: description));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankAdditionalDescription_ShouldFailNamingTheProblem(string description)
    {
        var request = BuildRequest(additional: [Issue("Fridge will not cool"), Issue(description)]);

        var result = IntakeIssuesValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Problem 3");
    }

    [Fact]
    public void Validate_DescriptionAtMaxLength_ShouldPass()
    {
        var request = BuildRequest(additional: [Issue(new string('a', IntakeIssuesValidator.MaxDescriptionLength))]);

        IntakeIssuesValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DescriptionOverMaxLength_ShouldFail()
    {
        var request = BuildRequest(additional: [Issue(new string('a', IntakeIssuesValidator.MaxDescriptionLength + 1))]);

        var result = IntakeIssuesValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Problem 2");
    }

    [Fact]
    public void Validate_NegativeExpectedAttachmentCount_ShouldPassSoOrchestrationCanClampIt()
    {
        var request = BuildRequest(additional: [Issue("Awning stuck") with { ExpectedAttachmentCount = -1 }]);

        IntakeIssuesValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullAdditionalIssueEntry_ShouldFail()
    {
        var request = BuildRequest(additional: [null!]);

        IntakeIssuesValidator.Validate(request).IsValid.Should().BeFalse();
    }

    private static IntakeIssueDto Issue(string description) => new()
    {
        IssueCategory = "Appliances",
        IssueDescription = description,
    };

    private static ServiceRequestCreateRequestDto BuildRequest(
        string leadDescription = "Slide will not retract",
        List<IntakeIssueDto>? additional = null) => new()
    {
        Customer = new CustomerInfoDto { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" },
        Asset = new AssetInfoDto { AssetId = "" },
        IssueCategory = "Slides",
        IssueDescription = leadDescription,
        AdditionalIssues = additional,
    };
}
