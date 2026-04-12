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
            Phone = "555-1234"
        };

        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Smith");
        dto.Email.Should().Be("john@example.com");
        dto.Phone.Should().Be("555-1234");
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
        dto.PrefillCustomer.Should().BeNull();
    }

    [Fact]
    public void CustomerStatusResponseDto_DefaultServiceRequestsIsEmpty()
    {
        var dto = new CustomerStatusResponseDto();

        dto.ServiceRequests.Should().BeEmpty();
    }
}
