using FluentAssertions;
using RVS.Domain.DTOs;

namespace RVS.Domain.Tests.DTOs;

public class CustomerIntakeDtoTests
{
    [Fact]
    public void CustomerInfoDto_SetsRequiredFields()
    {
        var dto = new CustomerInfoDto
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john@example.com",
            Phone = "555-1234",
            PreferredContact = "Phone"
        };

        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Smith");
        dto.Email.Should().Be("john@example.com");
        dto.Phone.Should().Be("555-1234");
        dto.PreferredContact.Should().Be("Phone");
    }

    [Fact]
    public void CustomerInfoDto_PreferredContactDefaultsToNull()
    {
        var dto = new CustomerInfoDto
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john@example.com"
        };

        dto.PreferredContact.Should().BeNull();
    }

    [Fact]
    public void AssetInfoDto_OptionalFieldsDefaultToNull()
    {
        var dto = new AssetInfoDto { AssetId = "1HGBH41JXMN109186" };

        dto.AssetId.Should().Be("1HGBH41JXMN109186");
        dto.Manufacturer.Should().BeNull();
        dto.Model.Should().BeNull();
        dto.Year.Should().BeNull();
    }

    [Fact]
    public void DiagnosticResponseDto_DefaultSelectedOptionsIsEmpty()
    {
        var dto = new DiagnosticResponseDto { QuestionText = "What is the issue?" };

        dto.SelectedOptions.Should().BeEmpty();
        dto.FreeTextResponse.Should().BeNull();
    }

    [Fact]
    public void DiagnosticQuestionDto_DefaultOptionsIsEmpty()
    {
        var dto = new DiagnosticQuestionDto { QuestionText = "Describe the issue" };

        dto.Options.Should().BeEmpty();
        dto.AllowFreeText.Should().BeFalse();
        dto.HelpText.Should().BeNull();
    }

    [Fact]
    public void DiagnosticQuestionsResponseDto_DefaultQuestionsIsEmpty()
    {
        var dto = new DiagnosticQuestionsResponseDto();

        dto.Questions.Should().BeEmpty();
        dto.SmartSuggestion.Should().BeNull();
    }

    [Fact]
    public void IntakeConfigResponseDto_DefaultCollectionsAreEmpty()
    {
        var dto = new IntakeConfigResponseDto();

        dto.AcceptedFileTypes.Should().BeEmpty();
        dto.IssueCategories.Should().BeEmpty();
    }

    [Fact]
    public void CustomerStatusResponseDto_DefaultServiceRequestsIsEmpty()
    {
        var dto = new CustomerStatusResponseDto();

        dto.ServiceRequests.Should().BeEmpty();
    }

    [Fact]
    public void CustomerStatusItemResponseDto_ExposesOnlyTheSpecX1C9AndC12Fields()
    {
        // Spec X-1 shows unit, submission date, current status, and the location's phone
        // number; Spec C-9 adds the manager-authored status note; issue #741 adds the issue
        // category's display name; issue #793 adds the servicing location's name and logo so
        // requests at different dealers are told apart; Spec C-12 (issue #844) adds the
        // scheduled date/time, already formatted in its booked zone. Nothing customer-identifying and no
        // free-text problem description crosses this boundary.
        var properties = typeof(CustomerStatusItemResponseDto)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n);

        properties.Should().Equal(
            "IssueCategory", "LocationLogoUrl", "LocationName", "LocationPhone", "ScheduledDisplay", "Status", "StatusNote", "SubmittedAtUtc", "Unit");
    }

    [Fact]
    public void CustomerStatusItemResponseDto_CanSetAllFields()
    {
        var submittedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var dto = new CustomerStatusItemResponseDto
        {
            Unit = "2023 Thor Ace",
            SubmittedAtUtc = submittedAt,
            Status = "InProgress",
            LocationPhone = "555-0100",
            StatusNote = "Waiting on a back-ordered slide motor, ETA Friday.",
            IssueCategory = "Slides",
            LocationName = "Salt Lake Service Center",
            LocationLogoUrl = "https://cdn.example.com/acme.png"
        };

        dto.Unit.Should().Be("2023 Thor Ace");
        dto.SubmittedAtUtc.Should().Be(submittedAt);
        dto.Status.Should().Be("InProgress");
        dto.LocationPhone.Should().Be("555-0100");
        dto.StatusNote.Should().Be("Waiting on a back-ordered slide motor, ETA Friday.");
        dto.IssueCategory.Should().Be("Slides");
        dto.LocationName.Should().Be("Salt Lake Service Center");
        dto.LocationLogoUrl.Should().Be("https://cdn.example.com/acme.png");
    }
}
