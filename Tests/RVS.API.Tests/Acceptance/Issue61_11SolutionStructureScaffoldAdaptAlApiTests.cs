using System.Xml.Linq;
using FluentAssertions;

namespace RVS.API.Tests.Acceptance;

/// <summary>
/// Acceptance tests for Issue 61 §1.1 — verifies the full solution structure,
/// project references, and absence of healthcare-specific packages.
/// </summary>
public class Issue61_11SolutionStructureScaffoldAdaptAlApiTests
{
    private static readonly string SolutionRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string SlnxPath = Path.Combine(SolutionRoot, "RVS.slnx");

    [Fact]
    public void SolutionFile_ShouldExist()
    {
        File.Exists(SlnxPath).Should().BeTrue("RVS.slnx must exist at the solution root");
    }

    [Theory]
    [InlineData("RVS.API/RVS.API.csproj")]
    [InlineData("RVS.Domain/RVS.Domain.csproj")]
    [InlineData("RVS.Infra.AzCosmosRepository/RVS.Infra.AzCosmosRepository.csproj")]
    [InlineData("RVS.Infra.AzBlobRepository/RVS.Infra.AzBlobRepository.csproj")]
    [InlineData("RVS.Infra.AzCredentials/RVS.Infra.AzCredentials.csproj")]
    [InlineData("RVS.Infra.AzTablesRepository/RVS.Infra.AzTablesRepository.csproj")]
    [InlineData("RVS.UI.Shared/RVS.UI.Shared.csproj")]
    [InlineData("RVS.Cust_Intake/RVS.Cust_Intake.csproj")]
    [InlineData("RVS.Mngr_Desktop/RVS.Mngr_Desktop.csproj")]
    [InlineData("RVS.Tech_Mobile/RVS.Tech_Mobile.csproj")]
    [InlineData("RVS.Data.Cosmos.Seed/RVS.Data.Cosmos.Seed.csproj")]
    [InlineData("Tests/RVS.API.Tests/RVS.API.Tests.csproj")]
    [InlineData("Tests/RVS.Domain.Tests/RVS.Domain.Tests.csproj")]
    [InlineData("Tests/RVS.Tests.Unit/RVS.Tests.Unit.csproj")]
    [InlineData("Tests/RVS.Tests.Integration/RVS.Tests.Integration.csproj")]
    public void SolutionFile_ShouldContainProject(string projectPath)
    {
        var slnxContent = File.ReadAllText(SlnxPath);

        slnxContent.Should().Contain(projectPath,
            $"RVS.slnx must include {projectPath}");
    }

    [Theory]
    [InlineData("RVS.API/RVS.API.csproj")]
    [InlineData("RVS.Domain/RVS.Domain.csproj")]
    [InlineData("RVS.UI.Shared/RVS.UI.Shared.csproj")]
    [InlineData("RVS.Cust_Intake/RVS.Cust_Intake.csproj")]
    [InlineData("RVS.Mngr_Desktop/RVS.Mngr_Desktop.csproj")]
    [InlineData("RVS.Tech_Mobile/RVS.Tech_Mobile.csproj")]
    [InlineData("RVS.Data.Cosmos.Seed/RVS.Data.Cosmos.Seed.csproj")]
    [InlineData("Tests/RVS.Tests.Unit/RVS.Tests.Unit.csproj")]
    [InlineData("Tests/RVS.Tests.Integration/RVS.Tests.Integration.csproj")]
    public void ProjectFile_ShouldExistOnDisk(string projectPath)
    {
        var fullPath = Path.Combine(SolutionRoot, projectPath);

        File.Exists(fullPath).Should().BeTrue($"{projectPath} must exist on disk");
    }

    [Fact]
    public void UIShared_ShouldBeReferencedByCustIntake()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Cust_Intake", "RVS.Cust_Intake.csproj");

        ProjectReferencesProject(csproj, "RVS.UI.Shared").Should().BeTrue(
            "RVS.Cust_Intake must reference RVS.UI.Shared");
    }

    [Fact]
    public void UIShared_ShouldBeReferencedByMngrDesktop()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Mngr_Desktop", "RVS.Mngr_Desktop.csproj");

        ProjectReferencesProject(csproj, "RVS.UI.Shared").Should().BeTrue(
            "RVS.Mngr_Desktop must reference RVS.UI.Shared");
    }

    [Fact]
    public void UIShared_ShouldBeReferencedByTechMobile()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Tech_Mobile", "RVS.Tech_Mobile.csproj");

        ProjectReferencesProject(csproj, "RVS.UI.Shared").Should().BeTrue(
            "RVS.Tech_Mobile must reference RVS.UI.Shared");
    }

    [Fact]
    public void Domain_ShouldBeReferencedByInfraCosmosRepository()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Infra.AzCosmosRepository",
            "RVS.Infra.AzCosmosRepository.csproj");

        ProjectReferencesProject(csproj, "RVS.Domain").Should().BeTrue(
            "RVS.Infra.AzCosmosRepository must reference RVS.Domain");
    }

    [Fact]
    public void Domain_ShouldBeReferencedByInfraBlobRepository()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Infra.AzBlobRepository",
            "RVS.Infra.AzBlobRepository.csproj");

        ProjectReferencesProject(csproj, "RVS.Domain").Should().BeTrue(
            "RVS.Infra.AzBlobRepository must reference RVS.Domain");
    }

    [Fact]
    public void API_ShouldReferenceInfraProjects()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.API", "RVS.API.csproj");

        ProjectReferencesProject(csproj, "RVS.Infra.AzCosmosRepository").Should().BeTrue(
            "RVS.API must reference RVS.Infra.AzCosmosRepository");
        ProjectReferencesProject(csproj, "RVS.Infra.AzBlobRepository").Should().BeTrue(
            "RVS.API must reference RVS.Infra.AzBlobRepository");
    }

    [Theory]
    [InlineData("RVS.API/RVS.API.csproj")]
    [InlineData("RVS.Domain/RVS.Domain.csproj")]
    [InlineData("RVS.Infra.AzCosmosRepository/RVS.Infra.AzCosmosRepository.csproj")]
    [InlineData("RVS.Infra.AzBlobRepository/RVS.Infra.AzBlobRepository.csproj")]
    public void Projects_ShouldNotContainHealthcarePackages(string projectPath)
    {
        var csproj = Path.Combine(SolutionRoot, projectPath);
        var doc = XDocument.Load(csproj);

        var packages = doc.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        packages.Should().NotContain(p => p.Contains("HL7", StringComparison.OrdinalIgnoreCase),
            $"{projectPath} must not reference HL7 packages");
        packages.Should().NotContain(p => p.Contains("FHIR", StringComparison.OrdinalIgnoreCase),
            $"{projectPath} must not reference FHIR packages");
        packages.Should().NotContain(p => p.Contains("Healthcare", StringComparison.OrdinalIgnoreCase),
            $"{projectPath} must not reference healthcare-specific packages");
    }

    [Fact]
    public void DataCosmosSeed_ShouldExistAndReferenceDomain()
    {
        var csproj = Path.Combine(SolutionRoot, "RVS.Data.Cosmos.Seed",
            "RVS.Data.Cosmos.Seed.csproj");

        File.Exists(csproj).Should().BeTrue("RVS.Data.Cosmos.Seed.csproj must exist");
        ProjectReferencesProject(csproj, "RVS.Domain").Should().BeTrue(
            "RVS.Data.Cosmos.Seed must reference RVS.Domain");
    }

    [Theory]
    [InlineData("RVS.API/RVS.API.csproj")]
    [InlineData("RVS.Domain/RVS.Domain.csproj")]
    [InlineData("RVS.UI.Shared/RVS.UI.Shared.csproj")]
    [InlineData("RVS.Cust_Intake/RVS.Cust_Intake.csproj")]
    [InlineData("RVS.Mngr_Desktop/RVS.Mngr_Desktop.csproj")]
    [InlineData("RVS.Tech_Mobile/RVS.Tech_Mobile.csproj")]
    public void Projects_ShouldTargetNet10(string projectPath)
    {
        var csproj = Path.Combine(SolutionRoot, projectPath);
        var doc = XDocument.Load(csproj);

        var tfm = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;

        tfm.Should().Be("net10.0", $"{projectPath} must target .NET 10");
    }

    private static bool ProjectReferencesProject(string csprojPath, string referencedProjectName)
    {
        var doc = XDocument.Load(csprojPath);

        return doc.Descendants("ProjectReference")
            .Any(e => (e.Attribute("Include")?.Value ?? string.Empty)
                .Contains(referencedProjectName, StringComparison.OrdinalIgnoreCase));
    }
}
