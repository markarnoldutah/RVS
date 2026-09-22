using System.Net;
using System.Text;
using FluentAssertions;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="TenantAccessGateHandler"/> — spots the API's "tenant disabled" 403 on any
/// manager call and records it in <see cref="TenantAccessState"/>, so the app can replace every
/// page with one clear message instead of a raw 403 on each (issue #625).
/// </summary>
public class TenantAccessGateHandlerTests
{
    private const string DisabledBody =
        """{"message":"Tenant disabled","errorId":"e1","code":"tenant_disabled","disabledMessage":"On hold.","supportContactEmail":"help@example.com"}""";

    private static (HttpClient Client, TenantAccessState State) Create(HttpStatusCode status, string? body)
    {
        var state = new TenantAccessState();
        var handler = new TenantAccessGateHandler(state)
        {
            InnerHandler = new StubHandler(status, body)
        };
        return (new HttpClient(handler) { BaseAddress = new Uri("https://api.example/") }, state);
    }

    [Fact]
    public void Constructor_WhenStateIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new TenantAccessGateHandler(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_WhenTenantDisabled403_ShouldMarkRestrictedWithMessageAndContact()
    {
        var (client, state) = Create(HttpStatusCode.Forbidden, DisabledBody);

        await client.GetAsync("api/locations");

        state.IsRestricted.Should().BeTrue();
        state.DisabledMessage.Should().Be("On hold.");
        state.SupportContactEmail.Should().Be("help@example.com");
    }

    [Fact]
    public async Task SendAsync_WhenTenantDisabled403_ShouldStillReturnTheResponseWithReadableBody()
    {
        var (client, _) = Create(HttpStatusCode.Forbidden, DisabledBody);

        var response = await client.GetAsync("api/locations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Be(DisabledBody);
    }

    [Theory]
    [InlineData("""{"message":"Forbidden","errorId":"e1"}""")]
    [InlineData("""{"message":"Tenant context is missing from the access token.","errorId":"e1"}""")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    public async Task SendAsync_WhenOther403_ShouldNotMarkRestricted(string? body)
    {
        var (client, state) = Create(HttpStatusCode.Forbidden, body);

        await client.GetAsync("api/admin/tenants");

        state.IsRestricted.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_WhenNot403_ShouldNotMarkRestricted(HttpStatusCode status)
    {
        var (client, state) = Create(status, DisabledBody);

        await client.GetAsync("api/locations");

        state.IsRestricted.Should().BeFalse();
    }

    private sealed class StubHandler(HttpStatusCode status, string? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
