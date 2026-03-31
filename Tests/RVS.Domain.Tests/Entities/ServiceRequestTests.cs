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
}
