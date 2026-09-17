using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="GoController"/> — the <c>go.rvintake.com</c> redirect that every
/// distribution path routes through (<c>Spec A-13</c>, issue #599).
/// </summary>
public class GoControllerTests
{
    private const string Slug = "nova-hurricane";
    private const string TargetUrl = "https://rvintake.com/nova-hurricane?src=qr";

    private readonly Mock<IIntakeRedirectService> _serviceMock = new();
    private readonly GoController _sut;

    public GoControllerTests()
    {
        _serviceMock.Setup(s => s.ResolveAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeRedirectResultDto(TargetUrl, "qr", true));

        _sut = new GoController(_serviceMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task RedirectToIntake_ShouldReturnATemporaryRedirectToTheResolvedTarget()
    {
        var result = await _sut.RedirectToIntake(Slug, "qr", CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(TargetUrl);
        // Never permanent: a 301 would be cached by the client and later taps would never
        // reach the hit log at all.
        redirect.Permanent.Should().BeFalse();
    }

    [Fact]
    public async Task RedirectToIntake_ShouldTellCachesNotToStoreTheResponse()
    {
        await _sut.RedirectToIntake(Slug, "qr", CancellationToken.None);

        _sut.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task RedirectToIntake_ShouldPassTheSourceAndUserAgentThrough()
    {
        _sut.Request.Headers.UserAgent = "facebookexternalhit/1.1";

        await _sut.RedirectToIntake(Slug, "textrepl", CancellationToken.None);

        _serviceMock.Verify(s => s.ResolveAsync(
            Slug, "textrepl", "facebookexternalhit/1.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RedirectToIntake_WhenNoSourceSupplied_ShouldPassNullAndLetTheServiceDefaultIt()
    {
        await _sut.RedirectToIntake(Slug, null, CancellationToken.None);

        _serviceMock.Verify(s => s.ResolveAsync(
            Slug, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RedirectToIntake_WhenSourceUnknown_ShouldStillRedirect()
    {
        _serviceMock.Setup(s => s.ResolveAsync(Slug, "nfc", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeRedirectResultDto("https://rvintake.com/nova-hurricane?src=nfc", "nfc", true));

        var result = await _sut.RedirectToIntake(Slug, "nfc", CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("https://rvintake.com/nova-hurricane?src=nfc");
    }

    [Fact]
    public async Task RedirectToIntake_WhenSlugDoesNotResolve_ShouldStillRedirectRatherThanReturn404()
    {
        // The intake app owns the "no such location" page; a 404 here would turn a typo on a
        // printed sticker into a dead link.
        _serviceMock.Setup(s => s.ResolveAsync("gone", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeRedirectResultDto("https://rvintake.com/gone?src=print", "print", false));

        var result = await _sut.RedirectToIntake("gone", null, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("https://rvintake.com/gone?src=print");
    }
}
