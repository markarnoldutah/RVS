using FluentAssertions;
using RVS.Blazor.Manager.Shared;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// The mobile request page the packet email links to (<c>Spec C-7</c>, issue #743): the status
/// vocabulary it offers, the update request it sends, the call / text / email links it builds for
/// the customer, and the header lines it shows.
/// </summary>
public class ServiceRequestMobileViewTests
{
    private static ServiceRequestDetailResponseDto Request() => new()
    {
        Id = "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
        TenantId = "ten_acme_rv",
        Status = "New",
        LocationId = "loc_1",
        Customer = new CustomerInfoDto
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "(801) 555-1234",
            PreferredContact = "Text",
        },
        Asset = new AssetInfoDto { AssetId = "1FUJA6CK12LM12345", Manufacturer = "Grand Design", Model = "Momentum", Year = 2021 },
        IssueCategory = "Slideouts",
        IssueDescription = "Slide will not retract.",
        TechnicianSummary = "[System — Sep 1, 2026 9:00:00 AM]\nStatus changed to New",
        Urgency = "Soon",
        RvUsage = "FullTime",
        HasExtendedWarranty = "Yes",
        ApproxPurchaseDate = "2021-06",
        Priority = "High",
        AssignedTechnicianId = "tech_7",
        ScheduledDateUtc = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc),
        RequiredSkills = ["hydraulics"],
        BoardSequence = 4,
    };

    // ---- Statuses ---------------------------------------------------------------

    [Fact]
    public void Statuses_ShouldBeTheC3SetInPathOrder()
    {
        ServiceRequestMobileView.Statuses.Select(s => s.Status).Should().Equal(
            "New", "InProgress", "WaitingOnParts", "WaitingOnCustomer", "Completed", "Cancelled");
    }

    [Theory]
    [InlineData("New", "New")]
    [InlineData("InProgress", "In Progress")]
    [InlineData("WaitingOnParts", "Waiting on Parts")]
    [InlineData("WaitingOnCustomer", "Waiting on Customer")]
    [InlineData("Completed", "Completed")]
    [InlineData("Cancelled", "Cancelled")]
    public void GetStatusLabel_ForKnownStatus_ShouldReturnSpecLabel(string status, string expected)
    {
        ServiceRequestMobileView.GetStatusLabel(status).Should().Be(expected);
    }

    [Fact]
    public void GetStatusLabel_ForUnknownStatus_ShouldEchoIt()
    {
        ServiceRequestMobileView.GetStatusLabel("Mystery").Should().Be("Mystery");
    }

    // ---- BuildUpdateRequest -----------------------------------------------------

    [Fact]
    public void BuildUpdateRequest_WhenRequestNull_ShouldThrowArgumentNullException()
    {
        var act = () => ServiceRequestMobileView.BuildUpdateRequest(null!, "summary");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildUpdateRequest_ShouldCarryForwardEveryOverwrittenField()
    {
        // ApplyUpdate is a full overwrite: any field left off the request is written back as
        // null. The email's one-tap confirm used to send four fields and wiped the rest.
        var sr = Request();

        var request = ServiceRequestMobileView.BuildUpdateRequest(sr, "new summary");

        request.Status.Should().Be("New");
        request.IssueDescription.Should().Be(sr.IssueDescription);
        request.IssueCategory.Should().Be("Slideouts");
        request.TechnicianSummary.Should().Be("new summary");
        request.Priority.Should().Be("High");
        request.Urgency.Should().Be("Soon");
        request.RvUsage.Should().Be("FullTime");
        request.HasExtendedWarranty.Should().Be("Yes");
        request.ApproxPurchaseDate.Should().Be("2021-06");
        request.AssignedTechnicianId.Should().Be("tech_7");
        request.ScheduledDateUtc.Should().Be(sr.ScheduledDateUtc);
        request.RequiredSkills.Should().Equal("hydraulics");
        request.BoardSequence.Should().Be(4);
    }

    [Fact]
    public void BuildUpdateRequest_ShouldLeaveCustomerAndAssetUnset()
    {
        // Null Customer / Asset means "unchanged" to ApplyUpdate.
        var request = ServiceRequestMobileView.BuildUpdateRequest(Request(), null);

        request.Customer.Should().BeNull();
        request.Asset.Should().BeNull();
    }

    [Fact]
    public void BuildUpdateRequest_WhenPriorityMissing_ShouldDefaultToMedium()
    {
        var request = ServiceRequestMobileView.BuildUpdateRequest(Request() with { Priority = null }, null);

        request.Priority.Should().Be("Medium");
    }

    [Fact]
    public void BuildUpdateRequest_ShouldNotShareTheSkillsList()
    {
        var sr = Request();

        var request = ServiceRequestMobileView.BuildUpdateRequest(sr, null);

        request.RequiredSkills.Should().NotBeSameAs(sr.RequiredSkills);
    }

    // ---- BuildContactLinks ------------------------------------------------------

    [Fact]
    public void BuildContactLinks_WhenCustomerNull_ShouldThrowArgumentNullException()
    {
        var act = () => ServiceRequestMobileView.BuildContactLinks(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildContactLinks_WithPhoneAndEmail_ShouldOfferCallTextAndEmail()
    {
        var links = ServiceRequestMobileView.BuildContactLinks(Request().Customer);

        links.Select(l => (l.Method, l.Href)).Should().Equal(
            ("Phone", "tel:+18015551234"),
            ("Text", "sms:+18015551234"),
            ("Email", "mailto:jane@example.com"));
    }

    [Fact]
    public void BuildContactLinks_ShouldMarkOnlyThePreferredMethod()
    {
        var links = ServiceRequestMobileView.BuildContactLinks(Request().Customer);

        links.Where(l => l.IsPreferred).Select(l => l.Method).Should().Equal("Text");
    }

    [Fact]
    public void BuildContactLinks_WithoutPhone_ShouldOfferEmailOnly()
    {
        var customer = Request().Customer with { Phone = null };

        var links = ServiceRequestMobileView.BuildContactLinks(customer);

        links.Select(l => l.Method).Should().Equal("Email");
    }

    [Fact]
    public void BuildContactLinks_WithNonNanpPhone_ShouldDialItsDigitsAsEntered()
    {
        var customer = Request().Customer with { Phone = "+44 20 7946 0958" };

        var links = ServiceRequestMobileView.BuildContactLinks(customer);

        links.First(l => l.Method == "Phone").Href.Should().Be("tel:+442079460958");
    }

    [Fact]
    public void BuildContactLinks_WithPhoneWithoutDigits_ShouldOmitCallAndText()
    {
        var customer = Request().Customer with { Phone = "n/a" };

        var links = ServiceRequestMobileView.BuildContactLinks(customer);

        links.Select(l => l.Method).Should().Equal("Email");
    }

    [Fact]
    public void BuildContactLinks_ShouldEscapeTheEmailAddress()
    {
        var customer = Request().Customer with { Email = "jane+rv@example.com" };

        var links = ServiceRequestMobileView.BuildContactLinks(customer);

        links.Single(l => l.Method == "Email").Href.Should().Be("mailto:jane%2Brv@example.com");
    }

    // ---- Header lines -----------------------------------------------------------

    [Theory]
    [InlineData("a1b2c3d4-e5f6-7890", "A1B2C3D4")]
    [InlineData("nohyphen", "NOHYPHEN")]
    public void GetReferenceCode_ShouldMatchThePacketsCode(string id, string expected)
    {
        ServiceRequestMobileView.GetReferenceCode(id).Should().Be(expected);
    }

    [Fact]
    public void GetUnitLine_WithYearMakeModel_ShouldJoinThem()
    {
        ServiceRequestMobileView.GetUnitLine(Request().Asset).Should().Be("2021 Grand Design Momentum");
    }

    [Fact]
    public void GetUnitLine_WithNothingKnown_ShouldReturnNull()
    {
        ServiceRequestMobileView.GetUnitLine(new AssetInfoDto { AssetId = "" }).Should().BeNull();
    }

    [Fact]
    public void GetUnitLine_WhenAssetNull_ShouldReturnNull()
    {
        ServiceRequestMobileView.GetUnitLine(null).Should().BeNull();
    }

    [Fact]
    public void GetCustomerName_ShouldJoinFirstAndLast()
    {
        ServiceRequestMobileView.GetCustomerName(Request().Customer).Should().Be("Jane Doe");
    }
}
