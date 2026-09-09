using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="ServiceRequest"/> entity conventions.
/// </summary>
public class ServiceRequestTests
{
    [Fact]
    public void NewServiceRequest_IdShouldBeValidGuid()
    {
        var sr = new ServiceRequest();

        Guid.TryParse(sr.Id, out _).Should().BeTrue();
    }

    [Fact]
    public void NewServiceRequest_TypeShouldBeServiceRequest()
    {
        var sr = new ServiceRequest();

        sr.Type.Should().Be("serviceRequest");
    }

    [Fact]
    public void NewServiceRequest_ShouldHaveEmptyDiagnosticResponses()
    {
        var sr = new ServiceRequest();

        sr.DiagnosticResponses.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void NewServiceRequest_ShouldHaveDefaultStatus()
    {
        var sr = new ServiceRequest();

        sr.Status.Should().Be("New");
    }

    [Fact]
    public void NewServiceRequest_ShouldHaveNoCustomerStatusNote()
    {
        var sr = new ServiceRequest();

        sr.CustomerStatusNote.Should().BeNull();
    }

    [Fact]
    public void SetCustomerStatusNote_WithText_StoresTrimmedTextAndAuditFields()
    {
        var sr = new ServiceRequest();
        var before = DateTime.UtcNow;

        sr.SetCustomerStatusNote("  Parts arrived — tech starts Monday.  ", "auth0|advisor-1");

        sr.CustomerStatusNote.Should().NotBeNull();
        sr.CustomerStatusNote!.Text.Should().Be("Parts arrived — tech starts Monday.");
        sr.CustomerStatusNote.UpdatedByUserId.Should().Be("auth0|advisor-1");
        sr.CustomerStatusNote.UpdatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void SetCustomerStatusNote_WithText_StampsEntityAsUpdated()
    {
        var sr = new ServiceRequest();

        sr.SetCustomerStatusNote("Waiting on customer approval.", "auth0|advisor-1");

        sr.UpdatedAtUtc.Should().NotBeNull();
        sr.UpdatedByUserId.Should().Be("auth0|advisor-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetCustomerStatusNote_WithBlank_ClearsExistingNoteAndStampsUpdated(string? note)
    {
        var sr = new ServiceRequest();
        sr.SetCustomerStatusNote("An earlier note.", "auth0|advisor-1");

        sr.SetCustomerStatusNote(note, "auth0|advisor-2");

        sr.CustomerStatusNote.Should().BeNull();
        sr.UpdatedByUserId.Should().Be("auth0|advisor-2");
    }
}
