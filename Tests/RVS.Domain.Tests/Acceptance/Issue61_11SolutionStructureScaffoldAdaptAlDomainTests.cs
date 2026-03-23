using System.Xml.Linq;
using FluentAssertions;

namespace RVS.Domain.Tests.Acceptance;

/// <summary>
/// Acceptance tests for Issue 61 §1.1 — verifies the RVS.Domain project is
/// correctly structured with no healthcare-specific packages.
/// </summary>
public class Issue61_11SolutionStructureScaffoldAdaptAlDomainTests
{
    private static readonly string SolutionRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void DomainProject_ShouldExist()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Domain", "RVS.Domain.csproj");

        File.Exists(csproj).Should().BeTrue("RVS.Domain.csproj must exist in the solution");
    }

    [Fact]
    public void DomainProject_ShouldTargetNet10()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Domain", "RVS.Domain.csproj");
        var doc = XDocument.Load(csproj);

        var tfm = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;

        tfm.Should().Be("net10.0", "Domain must target .NET 10");
    }

    [Fact]
    public void DomainProject_ShouldHaveNullableEnabled()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Domain", "RVS.Domain.csproj");
        var doc = XDocument.Load(csproj);

        var nullable = doc.Descendants("Nullable").FirstOrDefault()?.Value;

        nullable.Should().Be("enable", "Domain must have nullable reference types enabled");
    }

    [Fact]
    public void DomainProject_ShouldNotContainHealthcarePackages()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Domain", "RVS.Domain.csproj");
        var doc = XDocument.Load(csproj);

        var packages = doc.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        packages.Should().NotContain(p => p.Contains("HL7", StringComparison.OrdinalIgnoreCase),
            "Domain must not reference HL7 packages");
        packages.Should().NotContain(p => p.Contains("FHIR", StringComparison.OrdinalIgnoreCase),
            "Domain must not reference FHIR packages");
        packages.Should().NotContain(p => p.Contains("Healthcare", StringComparison.OrdinalIgnoreCase),
            "Domain must not reference healthcare-specific packages");
    }

    [Fact]
    public void DomainProject_ShouldContainEntityBase()
    {
        var entityBase = Path.Combine(SolutionRoot, "RVS.Domain", "Entities", "EntityBase.cs");

        File.Exists(entityBase).Should().BeTrue("EntityBase.cs must exist in Domain/Entities");
    }

    [Fact]
    public void DomainProject_EntitiesDirectory_ShouldExist()
    {
        var entitiesDir = Path.Combine(SolutionRoot, "RVS.Domain", "Entities");

        Directory.Exists(entitiesDir).Should().BeTrue("Domain/Entities directory must exist");
    }

    [Fact]
    public void DomainProject_InterfacesDirectory_ShouldExist()
    {
        var interfacesDir = Path.Combine(SolutionRoot, "RVS.Domain", "Interfaces");

        Directory.Exists(interfacesDir).Should().BeTrue("Domain/Interfaces directory must exist");
    }

    [Fact]
    public void DomainProject_DTOsDirectory_ShouldExist()
    {
        var dtosDir = Path.Combine(SolutionRoot, "RVS.Domain", "DTOs");

        Directory.Exists(dtosDir).Should().BeTrue("Domain/DTOs directory must exist");
    }

    [Fact]
    public void DomainProject_ValidationDirectory_ShouldExist()
    {
        var validationDir = Path.Combine(SolutionRoot, "RVS.Domain", "Validation");

        Directory.Exists(validationDir).Should().BeTrue("Domain/Validation directory must exist");
    }
}
