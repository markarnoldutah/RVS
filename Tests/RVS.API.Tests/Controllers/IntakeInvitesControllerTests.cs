using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="IntakeInvitesController"/> (<c>Spec A-14</c>, issue #663).
/// </summary>
public class IntakeInvitesControllerTests
{
    private const string TenantId = "ten_test";
    private const string LocationId = "loc_slc";

    private readonly Mock<IIntakeInviteService> _serviceMock = new();
    private readonly IntakeInvitesController _sut;

    public IntakeInvitesControllerTests()
    {
        _sut = new IntakeInvitesController(_serviceMock.Object, BuildClaimsService(TenantId));
    }

    [Fact]
    public void Controller_ShouldRequireTheSendIntakeInvitesPolicyOnEveryAction()
    {
        // Being signed in to the manager app is not enough (Spec A-14).
        var actions = typeof(IntakeInvitesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        actions.Should().NotBeEmpty();
        actions.Should().AllSatisfy(m =>
            m.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("CanSendIntakeInvites"));
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtActionWithTheDetailDto()
    {
        var request = new IntakeInviteCreateRequestDto { FirstName = "Jane", Phone = "8015551234", ConsentCaptured = true };
        var invite = BuildInvite();
        _serviceMock.Setup(s => s.CreateAsync(TenantId, LocationId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeInviteCreateResult(invite, null));

        var result = await _sut.Create(LocationId, request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(IntakeInvitesController.GetById));
        created.RouteValues.Should().Contain("locationId", LocationId).And.Contain("id", invite.Id);
        var dto = created.Value.Should().BeOfType<IntakeInviteDetailResponseDto>().Subject;
        dto.Id.Should().Be(invite.Id);
        dto.FirstName.Should().Be("Jane");
        dto.Phone.Should().Be("+18015551234");
        dto.DeliveryStatus.Should().Be(IntakeInviteDeliveryStatus.Queued);
        dto.IntakeUrl.Should().BeNull();
    }

    [Fact]
    public async Task Create_SelfEntry_ShouldReturnTheIntakeUrl()
    {
        var request = new IntakeInviteCreateRequestDto { FirstName = "Jane", SelfEntry = true };
        const string url = "https://rvintake.com/slc?src=advisor&inv=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        _serviceMock.Setup(s => s.CreateAsync(TenantId, LocationId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeInviteCreateResult(BuildInvite(), url));

        var result = await _sut.Create(LocationId, request, CancellationToken.None);

        var dto = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject
            .Value.Should().BeOfType<IntakeInviteDetailResponseDto>().Subject;
        dto.IntakeUrl.Should().Be(url);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithTheDetailDtoAndNoUrl()
    {
        var invite = BuildInvite();
        _serviceMock.Setup(s => s.GetByIdAsync(TenantId, LocationId, invite.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await _sut.GetById(LocationId, invite.Id, CancellationToken.None);

        var dto = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<IntakeInviteDetailResponseDto>().Subject;
        dto.Id.Should().Be(invite.Id);
        dto.IntakeUrl.Should().BeNull();
    }

    [Fact]
    public async Task ListRecent_ShouldReturnOkWithSummaryDtos()
    {
        _serviceMock.Setup(s => s.ListRecentForCurrentAdvisorAsync(TenantId, LocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildInvite(), BuildInvite()]);

        var result = await _sut.ListRecent(LocationId, CancellationToken.None);

        var dtos = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<IntakeInviteSummaryResponseDto>>().Subject;
        dtos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_WhenTenantClaimIsMissing_ShouldThrowUnauthorizedAccessException()
    {
        var sut = new IntakeInvitesController(_serviceMock.Object, BuildClaimsService(null));

        var act = () => sut.Create(LocationId, new IntakeInviteCreateRequestDto { FirstName = "Jane" }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static IntakeInvite BuildInvite() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        TenantId = TenantId,
        LocationId = LocationId,
        AdvisorUserId = "usr_test",
        FirstName = "Jane",
        Phone = "+18015551234",
        ConsentCapturedAtUtc = DateTime.UtcNow,
        SentAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddHours(72),
        AcsMessageId = "Outgoing_abc",
        DeliveryStatus = IntakeInviteDeliveryStatus.Queued,
    };

    private static ClaimsService BuildClaimsService(string? tenantId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "usr_test") };
        if (tenantId is not null)
        {
            claims.Add(new Claim(ClaimsService.TenantIdClaimType, tenantId));
        }

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
