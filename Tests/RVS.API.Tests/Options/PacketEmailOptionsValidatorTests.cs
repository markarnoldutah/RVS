using FluentAssertions;
using RVS.API.Options;
using RVS.Domain.Packets;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Options;

/// <summary>
/// Tests for <see cref="PacketEmailOptionsValidator"/> — the <c>IValidateOptions</c> adapter
/// that runs <see cref="PacketEmailBudgetValidator"/> against the bound <c>PacketEmail</c>
/// section at startup (issue #521). The rules themselves are covered in the Domain tests; this
/// proves the adapter passes them through faithfully.
/// </summary>
public class PacketEmailOptionsValidatorTests
{
    private readonly PacketEmailOptionsValidator _sut = new();

    [Fact]
    public void Validate_WhenOptionsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.Validate(name: null, options: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_DefaultOptions_ShouldSucceed()
    {
        var result = _sut.Validate(name: null, new PacketEmailOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_BudgetBelowTheMinimum_ShouldFailWithTheDomainMessage()
    {
        var options = new PacketEmailOptions { MaxRequestBytes = PacketEmailBudgetValidator.MinimumMaxRequestBytes - 1 };

        var result = _sut.Validate(name: null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Be(PacketEmailBudgetValidator.Validate(options.MaxRequestBytes).ErrorMessage);
    }

    [Fact]
    public void Validate_BudgetAboveTheAcsCeiling_ShouldFail()
    {
        var options = new PacketEmailOptions { MaxRequestBytes = PacketEmailSizeFitter.AcsMaxRequestBytes + 1 };

        var result = _sut.Validate(name: null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PacketEmail:MaxRequestBytes");
    }
}
