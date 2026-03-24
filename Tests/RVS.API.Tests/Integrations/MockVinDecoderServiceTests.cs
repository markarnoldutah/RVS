using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class MockVinDecoderServiceTests
{
    private readonly MockVinDecoderService _sut = new(Mock.Of<ILogger<MockVinDecoderService>>());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DecodeVinAsync_WhenVinIsNullOrWhiteSpace_ShouldThrowArgumentException(string? vin)
    {
        var act = () => _sut.DecodeVinAsync(vin!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecodeVinAsync_ShouldReturnGrandDesignMomentumData()
    {
        var result = await _sut.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
        result.Manufacturer.Should().Be("Grand Design");
        result.Model.Should().Be("Momentum 395MS");
        result.Year.Should().Be(2024);
    }
}
