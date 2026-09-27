using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="LocationBrandingEmbedded"/> on <see cref="Location"/> (<c>Spec A-16</c>,
/// issue #470), including the read-time migration of the logo out of
/// <c>packetConfig.logoUrl</c> — its home from #435 until branding had one of its own.
/// </summary>
public class LocationBrandingEmbeddedTests
{
    [Fact]
    public void NewLocation_ShouldHaveEmptyBrandingSoTheProductDefaultsApply()
    {
        var location = new Location();

        location.Branding.Should().NotBeNull();
        location.Branding.LogoUrl.Should().BeNull();
        location.Branding.HeaderColor.Should().BeNull();
        location.Branding.HasLogo.Should().BeFalse();
        location.Branding.HasHeaderColor.Should().BeFalse();
    }

    [Fact]
    public void Serialize_ShouldWriteBrandingUnderCamelCaseNames()
    {
        var location = new Location
        {
            Branding = new LocationBrandingEmbedded
            {
                LogoUrl = "https://cdn.dealer.example/logo.png",
                HeaderColor = "#1A5E20",
            },
        };

        var json = JObject.Parse(JsonConvert.SerializeObject(location));

        json["branding"]!["logoUrl"]!.Value<string>().Should().Be("https://cdn.dealer.example/logo.png");
        json["branding"]!["headerColor"]!.Value<string>().Should().Be("#1A5E20");
    }

    // ── Migration: packetConfig.logoUrl → branding.logoUrl ──────────────────

    [Fact]
    public void Deserialize_WhenOnlyTheLegacyPacketLogoIsSet_ShouldMoveItIntoBranding()
    {
        const string json = """
            { "id": "loc_1", "tenantId": "ten_1", "packetConfig": { "logoUrl": "https://cdn.dealer.example/old.png" } }
            """;

        var location = JsonConvert.DeserializeObject<Location>(json)!;

        location.Branding.LogoUrl.Should().Be("https://cdn.dealer.example/old.png");
    }

    [Fact]
    public void Deserialize_WhenBrandingAlreadyHasALogo_ShouldKeepItOverTheLegacyOne()
    {
        const string json = """
            {
              "id": "loc_1", "tenantId": "ten_1",
              "branding": { "logoUrl": "https://cdn.dealer.example/new.png" },
              "packetConfig": { "logoUrl": "https://cdn.dealer.example/old.png" }
            }
            """;

        var location = JsonConvert.DeserializeObject<Location>(json)!;

        location.Branding.LogoUrl.Should().Be("https://cdn.dealer.example/new.png");
    }

    [Fact]
    public void Deserialize_WhenTheLegacyPacketLogoIsNotHttps_ShouldDropIt()
    {
        // #435 accepted http; branding does not, because the intake page is https and a
        // mixed-content logo would never load. Carrying it over would make the location
        // unsaveable until someone found and cleared a field they can no longer see.
        const string json = """
            { "id": "loc_1", "tenantId": "ten_1", "packetConfig": { "logoUrl": "http://cdn.dealer.example/old.png" } }
            """;

        var location = JsonConvert.DeserializeObject<Location>(json)!;

        location.Branding.LogoUrl.Should().BeNull();
    }

    [Fact]
    public void Serialize_AfterMigration_ShouldNotWriteTheLegacyPacketLogoBack()
    {
        const string json = """
            { "id": "loc_1", "tenantId": "ten_1", "packetConfig": { "logoUrl": "https://cdn.dealer.example/old.png" } }
            """;
        var location = JsonConvert.DeserializeObject<Location>(json)!;

        var written = JObject.Parse(JsonConvert.SerializeObject(location));

        written["packetConfig"]!["logoUrl"].Should().BeNull();
        written["branding"]!["logoUrl"]!.Value<string>().Should().Be("https://cdn.dealer.example/old.png");
    }

    [Fact]
    public void Deserialize_WhenNoBrandingIsStored_ShouldDefaultToEmptyBranding()
    {
        const string json = """{ "id": "loc_1", "tenantId": "ten_1" }""";

        var location = JsonConvert.DeserializeObject<Location>(json)!;

        location.Branding.Should().NotBeNull();
        location.Branding.HasLogo.Should().BeFalse();
    }
}
