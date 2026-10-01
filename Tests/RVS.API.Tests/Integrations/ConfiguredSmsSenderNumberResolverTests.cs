using FluentAssertions;
using RVS.API.Integrations;
using RVS.API.Options;

namespace RVS.API.Tests.Integrations;

public class ConfiguredSmsSenderNumberResolverTests
{
    private static ConfiguredSmsSenderNumberResolver CreateResolver(string fromPhoneNumber) =>
        new(Microsoft.Extensions.Options.Options.Create(new SmsOptions { FromPhoneNumber = fromPhoneNumber }));

    [Theory]
    [InlineData("loc_slc")]
    [InlineData("loc_boise")]
    public async Task ResolveAsync_ForAnyLocation_ShouldReturnTheConfiguredNumber(string locationId)
    {
        var sut = CreateResolver("+18662319618");

        var from = await sut.ResolveAsync("ten_test", locationId);

        from.Should().Be("+18662319618");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ResolveAsync_WhenNoNumberIsConfigured_ShouldReturnNull(string configured)
    {
        var sut = CreateResolver(configured);

        var from = await sut.ResolveAsync("ten_test", "loc_slc");

        from.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ResolveAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var sut = CreateResolver("+18662319618");

        var act = () => sut.ResolveAsync(tenantId!, "loc_slc");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ResolveAsync_WhenLocationIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? locationId)
    {
        var sut = CreateResolver("+18662319618");

        var act = () => sut.ResolveAsync("ten_test", locationId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
