using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Exceptions;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// Spec P-2 / P-3 / P-7 (issue #563): the Auth0 Management API client, driven against a fake
/// <see cref="HttpMessageHandler"/> that stands in for the tenant.
/// </summary>
public sealed class Auth0ManagementProvisionerTests
{
    private const string Domain = "nova-test.us.auth0.com";
    private const string ManagerBaseUrl = "https://manager.rvintake.com";
    private const string TicketUrl = "https://nova-test.us.auth0.com/lo/reset?ticket=s3cret#";

    private readonly FakeAuth0Handler _handler = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<Auth0ManagementProvisioner>> _loggerMock = new();
    private readonly Auth0ManagementTokenCache _tokenCache = new();

    // ── EnsureUserAsync (Spec P-2) ───────────────────────────────────────────

    [Fact]
    public async Task EnsureUserAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => CreateSut().EnsureUserAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnsureUserAsync_WhenEmailIsNew_ShouldCreateDatabaseUserWithRandomUnverifiedPassword()
    {
        var result = await CreateSut().EnsureUserAsync(OwnerRequest());

        result.UserId.Should().Be("auth0|new1");
        result.Created.Should().BeTrue();

        var create = _handler.Single(HttpMethod.Post, "/api/v2/users");
        using var body = JsonDocument.Parse(create.Body!);
        body.RootElement.GetProperty("connection").GetString().Should().Be("Username-Password-Authentication");
        body.RootElement.GetProperty("email").GetString().Should().Be("jay@nova.example.com");
        body.RootElement.GetProperty("name").GetString().Should().Be("Jay Lyons");
        body.RootElement.GetProperty("email_verified").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("verify_email").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("password").GetString()!.Length.Should().BeGreaterThanOrEqualTo(24);
    }

    [Fact]
    public async Task EnsureUserAsync_WhenEmailIsNew_ShouldSetAppMetadataForThePostLoginAction()
    {
        await CreateSut().EnsureUserAsync(OwnerRequest() with { Role = "dealer:manager", LocationIds = ["loc_nova_1"] });

        var create = _handler.Single(HttpMethod.Post, "/api/v2/users");
        using var body = JsonDocument.Parse(create.Body!);
        var metadata = body.RootElement.GetProperty("app_metadata");
        metadata.GetProperty("tenantId").GetString().Should().Be("ten_nova");
        metadata.GetProperty("orgName").GetString().Should().Be("Nova RV Services");
        metadata.GetProperty("locationIds").EnumerateArray().Select(e => e.GetString()).Should().Equal("loc_nova_1");
    }

    [Fact]
    public async Task EnsureUserAsync_ShouldAssignTheRequestedRoleByExactName()
    {
        await CreateSut().EnsureUserAsync(OwnerRequest() with { Role = "dealer:manager" });

        var assign = _handler.Single(HttpMethod.Post, "/api/v2/roles/rol_manager/users");
        using var body = JsonDocument.Parse(assign.Body!);
        body.RootElement.GetProperty("users").EnumerateArray().Select(e => e.GetString()).Should().Equal("auth0|new1");
    }

    [Fact]
    public async Task EnsureUserAsync_WhenRoleNotFound_ShouldThrowBeforeCreatingTheUser()
    {
        _handler.RolesJson = "[]";

        var act = () => CreateSut().EnsureUserAsync(OwnerRequest());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*dealer:owner*");
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Post && r.Path == "/api/v2/users");
    }

    [Fact]
    public async Task EnsureUserAsync_WhenEmailExistsInSameTenant_ShouldUpdateMetadataAndNotCreate()
    {
        _handler.UsersByEmailJson = ExistingUserJson("auth0|existing1", tenantId: "ten_nova");

        var result = await CreateSut().EnsureUserAsync(OwnerRequest() with { Role = "dealer:manager", LocationIds = ["loc_nova_2"] });

        result.UserId.Should().Be("auth0|existing1");
        result.Created.Should().BeFalse();
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Post && r.Path == "/api/v2/users");

        var patch = _handler.Single(HttpMethod.Patch, "/api/v2/users/auth0%7Cexisting1");
        using var body = JsonDocument.Parse(patch.Body!);
        body.RootElement.GetProperty("name").GetString().Should().Be("Jay Lyons");
        body.RootElement.GetProperty("app_metadata").GetProperty("tenantId").GetString().Should().Be("ten_nova");
        body.RootElement.GetProperty("app_metadata").GetProperty("locationIds")
            .EnumerateArray().Select(e => e.GetString()).Should().Equal("loc_nova_2");
        _handler.Single(HttpMethod.Post, "/api/v2/roles/rol_manager/users");
    }

    [Fact]
    public async Task EnsureUserAsync_WhenExistingUserHadAnotherDealerRole_ShouldReplaceItAndKeepOtherRoles()
    {
        // Re-adding someone with a new role used to leave them holding both, and permissions are
        // the union of roles, so a downgrade silently did nothing.
        _handler.UsersByEmailJson = ExistingUserJson("auth0|existing1", tenantId: "ten_nova");
        _handler.UserRolesJson =
            """[{"id":"rol_owner","name":"dealer:owner"},{"id":"rol_manager","name":"dealer:manager"},{"id":"rol_other_product","name":"acme:viewer"}]""";

        await CreateSut().EnsureUserAsync(OwnerRequest() with { Role = "dealer:manager", LocationIds = ["loc_nova_1"] });

        var remove = _handler.Single(HttpMethod.Delete, "/api/v2/users/auth0%7Cexisting1/roles");
        using var body = JsonDocument.Parse(remove.Body!);
        body.RootElement.GetProperty("roles").EnumerateArray().Select(e => e.GetString()).Should().Equal("rol_owner");
    }

    [Fact]
    public async Task EnsureUserAsync_WhenUserIsNew_ShouldNotLookUpOrRemoveRoles()
    {
        await CreateSut().EnsureUserAsync(OwnerRequest());

        _handler.Requests.Should().NotContain(r => r.Path.EndsWith("/roles") && r.Path.StartsWith("/api/v2/users/"));
    }

    [Fact]
    public async Task EnsureUserAsync_WhenEmailBelongsToAnotherTenant_ShouldThrowConflictAndChangeNothing()
    {
        _handler.UsersByEmailJson = ExistingUserJson("auth0|existing1", tenantId: "ten_other");

        var act = () => CreateSut().EnsureUserAsync(OwnerRequest());

        await act.Should().ThrowAsync<ConflictException>();
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Patch);
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Post && r.Path.StartsWith("/api/v2/"));
    }

    [Fact]
    public async Task EnsureUserAsync_WhenExistingDatabaseUserHasNoTenant_ShouldThrowConflict()
    {
        // The Auth0 tenant is shared with other products; a user without an RVS tenantId is not ours.
        _handler.UsersByEmailJson = ExistingUserJson("auth0|stranger", tenantId: null);

        var act = () => CreateSut().EnsureUserAsync(OwnerRequest());

        await act.Should().ThrowAsync<ConflictException>();
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Patch);
    }

    [Fact]
    public async Task EnsureUserAsync_WhenOnlyASocialUserHasTheEmail_ShouldStillCreateTheDatabaseUser()
    {
        _handler.UsersByEmailJson = ExistingUserJson("google-oauth2|123", tenantId: null, connection: "google-oauth2");

        var result = await CreateSut().EnsureUserAsync(OwnerRequest());

        result.Created.Should().BeTrue();
        _handler.Single(HttpMethod.Post, "/api/v2/users");
    }

    [Fact]
    public async Task EnsureUserAsync_ShouldLookUpByEmailWithBearerToken()
    {
        await CreateSut().EnsureUserAsync(OwnerRequest());

        var lookup = _handler.Single(HttpMethod.Get, "/api/v2/users-by-email");
        lookup.Uri.Query.Should().Contain("email=jay%40nova.example.com");
        lookup.Authorization.Should().Be("Bearer mgmt-token");
    }

    [Fact]
    public async Task EnsureUserAsync_WhenAuth0RejectsTheCreate_ShouldThrowWithAuth0MessageButNotThePassword()
    {
        _handler.CreateUserResponse = () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = Json("""{"statusCode":400,"error":"Bad Request","message":"PasswordStrengthError: Password is too weak"}"""),
        };

        var act = () => CreateSut().EnsureUserAsync(OwnerRequest());

        var ex = (await act.Should().ThrowAsync<HttpRequestException>()).Which;
        var create = _handler.Single(HttpMethod.Post, "/api/v2/users");
        using var body = JsonDocument.Parse(create.Body!);
        var password = body.RootElement.GetProperty("password").GetString()!;
        ex.Message.Should().Contain("PasswordStrengthError");
        ex.Message.Should().NotContain(password);
        VerifyNeverLogged(password);
    }

    // ── Client-credentials token ─────────────────────────────────────────────

    [Fact]
    public async Task Token_ShouldBeRequestedWithClientCredentialsForTheManagementAudience()
    {
        await CreateSut().GetUserAsync("auth0|u1");

        var token = _handler.Single(HttpMethod.Post, "/oauth/token");
        using var body = JsonDocument.Parse(token.Body!);
        body.RootElement.GetProperty("grant_type").GetString().Should().Be("client_credentials");
        body.RootElement.GetProperty("client_id").GetString().Should().Be("client-id");
        body.RootElement.GetProperty("client_secret").GetString().Should().Be("client-secret");
        body.RootElement.GetProperty("audience").GetString().Should().Be("https://nova-test.us.auth0.com/api/v2/");
    }

    [Fact]
    public async Task Token_ShouldBeCachedAcrossCallsAndInstances()
    {
        await CreateSut().GetUserAsync("auth0|u1");
        await CreateSut().GetUserAsync("auth0|u1");

        _handler.Requests.Count(r => r.Path == "/oauth/token").Should().Be(1);
    }

    [Fact]
    public async Task Token_WhenNearExpiry_ShouldBeRefreshed()
    {
        var sut = CreateSut();
        await sut.GetUserAsync("auth0|u1");

        _time.Advance(TimeSpan.FromSeconds(_handler.TokenLifetimeSeconds - 30));
        await sut.GetUserAsync("auth0|u1");

        _handler.Requests.Count(r => r.Path == "/oauth/token").Should().Be(2);
    }

    // ── GetUserAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserAsync_WhenFound_ShouldReturnTheUsersTenant()
    {
        var user = await CreateSut().GetUserAsync("auth0|u1");

        user.Should().NotBeNull();
        user!.UserId.Should().Be("auth0|u1");
        user.Email.Should().Be("sam@nova.example.com");
        user.TenantId.Should().Be("ten_nova");
    }

    [Fact]
    public async Task GetUserAsync_WhenNotFound_ShouldReturnNull()
    {
        _handler.GetUserResponse = () => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = Json("""{"statusCode":404,"message":"The user does not exist."}"""),
        };

        var user = await CreateSut().GetUserAsync("auth0|missing");

        user.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetUserAsync_WhenUserIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? userId)
    {
        var act = () => CreateSut().GetUserAsync(userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetUserAsync_ShouldMapProfileFieldsWithoutFetchingRoles()
    {
        _handler.GetUserResponse = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
                {"user_id":"auth0|u1","email":"sam@nova.example.com","name":"Sam Advisor","blocked":true,
                 "created_at":"2026-09-01T12:00:00.000Z","last_login":"2026-09-20T08:30:00.000Z",
                 "app_metadata":{"tenantId":"ten_nova","locationIds":["loc_1","loc_2"]}}
                """),
        };

        var user = await CreateSut().GetUserAsync("auth0|u1");

        user!.DisplayName.Should().Be("Sam Advisor");
        user.Blocked.Should().BeTrue();
        user.LocationIds.Should().Equal("loc_1", "loc_2");
        user.CreatedAtUtc.Should().Be(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
        user.LastLoginAtUtc.Should().Be(new DateTime(2026, 9, 20, 8, 30, 0, DateTimeKind.Utc));
        user.Roles.Should().BeEmpty();
        _handler.Requests.Should().NotContain(r => r.Path.EndsWith("/roles"));
    }

    // ── ListUsersAsync (Spec P-9) ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ListUsersAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => CreateSut().ListUsersAsync(tenantId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("ten_nova\" OR tenantId:*")]
    [InlineData("ten_nova OR *")]
    [InlineData("ten_*")]
    public async Task ListUsersAsync_WhenTenantIdCouldAlterTheSearchQuery_ShouldThrowBeforeCallingAuth0(string tenantId)
    {
        var act = () => CreateSut().ListUsersAsync(tenantId);

        await act.Should().ThrowAsync<ArgumentException>();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUsersAsync_ShouldSearchByTenantIdInAppMetadata()
    {
        await CreateSut().ListUsersAsync("ten_nova");

        var search = _handler.Requests.First(r => r.Method == HttpMethod.Get && r.Path == "/api/v2/users");
        var query = Uri.UnescapeDataString(search.Uri.Query);
        query.Should().Contain("q=app_metadata.tenantId:\"ten_nova\"");
        query.Should().Contain("search_engine=v3");
        query.Should().Contain("per_page=100");
    }

    [Fact]
    public async Task ListUsersAsync_ShouldReturnEachUserWithItsRoles()
    {
        _handler.SearchPages =
        [
            """
            [{"user_id":"auth0|jay","email":"jay@nova.example.com","name":"Jay Lyons","app_metadata":{"tenantId":"ten_nova"}},
             {"user_id":"auth0|sam","email":"sam@nova.example.com","name":"Sam Advisor","blocked":true,
              "app_metadata":{"tenantId":"ten_nova","locationIds":["loc_1"]}}]
            """,
        ];
        _handler.UserRolesByUser["auth0|jay"] = """[{"id":"rol_owner","name":"dealer:owner"}]""";
        _handler.UserRolesByUser["auth0|sam"] = """[{"id":"rol_advisor","name":"dealer:advisor"}]""";

        var users = await CreateSut().ListUsersAsync("ten_nova");

        users.Should().HaveCount(2);
        users[0].UserId.Should().Be("auth0|jay");
        users[0].Roles.Should().Equal("dealer:owner");
        users[0].Blocked.Should().BeFalse();
        users[1].Roles.Should().Equal("dealer:advisor");
        users[1].Blocked.Should().BeTrue();
        users[1].LocationIds.Should().Equal("loc_1");
        users[1].TenantId.Should().Be("ten_nova");
    }

    [Fact]
    public async Task ListUsersAsync_WhenAFullPageIsReturned_ShouldRequestTheNextPage()
    {
        var fullPage = "[" + string.Join(",", Enumerable.Range(0, 100).Select(i =>
            $$$"""{"user_id":"auth0|u{{{i}}}","email":"u{{{i}}}@nova.example.com","app_metadata":{"tenantId":"ten_nova"}}""")) + "]";
        _handler.SearchPages =
        [
            fullPage,
            """[{"user_id":"auth0|last","email":"last@nova.example.com","app_metadata":{"tenantId":"ten_nova"}}]""",
        ];

        var users = await CreateSut().ListUsersAsync("ten_nova");

        users.Should().HaveCount(101);
        _handler.Requests.Count(r => r.Method == HttpMethod.Get && r.Path == "/api/v2/users").Should().Be(2);
    }

    // ── UpdateUserAsync (Spec P-10) ──────────────────────────────────────────

    [Fact]
    public async Task UpdateUserAsync_WhenChangesIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => CreateSut().UpdateUserAsync("auth0|u1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldPatchNameAndLocationsOnlyLeavingTenantIdAlone()
    {
        await CreateSut().UpdateUserAsync("auth0|u1", new IdentityUserUpdate("Sam Manager", ["loc_2"], "dealer:manager"));

        var patch = _handler.Single(HttpMethod.Patch, "/api/v2/users/auth0%7Cu1");
        using var body = JsonDocument.Parse(patch.Body!);
        body.RootElement.GetProperty("name").GetString().Should().Be("Sam Manager");
        var metadata = body.RootElement.GetProperty("app_metadata");
        metadata.GetProperty("locationIds").EnumerateArray().Select(e => e.GetString()).Should().Equal("loc_2");

        // app_metadata PATCHes merge top-level keys; sending tenantId would let an edit move a user.
        metadata.TryGetProperty("tenantId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldAssignTheNewRoleThenRemoveOtherDealerRoles()
    {
        _handler.UserRolesJson =
            """[{"id":"rol_advisor","name":"dealer:advisor"},{"id":"rol_other_product","name":"acme:viewer"}]""";

        await CreateSut().UpdateUserAsync("auth0|u1", new IdentityUserUpdate("Sam", ["loc_1"], "dealer:manager"));

        var assign = _handler.Single(HttpMethod.Post, "/api/v2/roles/rol_manager/users");
        var remove = _handler.Single(HttpMethod.Delete, "/api/v2/users/auth0%7Cu1/roles");
        using var body = JsonDocument.Parse(remove.Body!);
        body.RootElement.GetProperty("roles").EnumerateArray().Select(e => e.GetString()).Should().Equal("rol_advisor");

        // Assign before remove, so a failure part-way never leaves the user with no role at all.
        _handler.Requests.IndexOf(assign).Should().BeLessThan(_handler.Requests.IndexOf(remove));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenRoleUnchanged_ShouldRemoveNothing()
    {
        _handler.UserRolesJson = """[{"id":"rol_manager","name":"dealer:manager"}]""";

        await CreateSut().UpdateUserAsync("auth0|u1", new IdentityUserUpdate("Sam", ["loc_1"], "dealer:manager"));

        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenRoleNotFound_ShouldThrowBeforeChangingTheUser()
    {
        _handler.RolesJson = "[]";

        var act = () => CreateSut().UpdateUserAsync("auth0|u1", new IdentityUserUpdate("Sam", [], "dealer:owner"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*dealer:owner*");
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Patch);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldReturnTheUserWithCurrentRoles()
    {
        _handler.UserRolesJson = """[{"id":"rol_manager","name":"dealer:manager"}]""";

        var user = await CreateSut().UpdateUserAsync("auth0|u1", new IdentityUserUpdate("Sam", ["loc_1"], "dealer:manager"));

        user.UserId.Should().Be("auth0|u1");
        user.Roles.Should().Equal("dealer:manager");
    }

    // ── SetBlockedAsync (Spec P-11) ──────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetBlockedAsync_ShouldPatchTheBlockedFlagOnly(bool blocked)
    {
        await CreateSut().SetBlockedAsync("auth0|u1", blocked);

        var patch = _handler.Single(HttpMethod.Patch, "/api/v2/users/auth0%7Cu1");
        using var body = JsonDocument.Parse(patch.Body!);
        body.RootElement.GetProperty("blocked").GetBoolean().Should().Be(blocked);
        body.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("blocked");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SetBlockedAsync_WhenUserIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? userId)
    {
        var act = () => CreateSut().SetBlockedAsync(userId!, blocked: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── DeleteUserAsync (Spec P-12) ──────────────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_ShouldDeleteTheUser()
    {
        await CreateSut().DeleteUserAsync("auth0|u1");

        _handler.Single(HttpMethod.Delete, "/api/v2/users/auth0%7Cu1");
    }

    [Fact]
    public async Task DeleteUserAsync_WhenAlreadyGone_ShouldSucceed()
    {
        _handler.DeleteUserStatus = HttpStatusCode.NotFound;

        var act = () => CreateSut().DeleteUserAsync("auth0|u1");

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteUserAsync_WhenUserIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? userId)
    {
        var act = () => CreateSut().DeleteUserAsync(userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── CreatePasswordTicketAsync (Spec P-2 step 4, P-3) ─────────────────────

    [Fact]
    public async Task CreatePasswordTicketAsync_ShouldRequestASevenDayVerifiedTicketReturningToTheManagerApp()
    {
        var ticket = await CreateSut().CreatePasswordTicketAsync("auth0|u1");

        ticket.Url.Should().Be(TicketUrl);
        ticket.ExpiresAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddDays(7));

        var request = _handler.Single(HttpMethod.Post, "/api/v2/tickets/password-change");
        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetProperty("user_id").GetString().Should().Be("auth0|u1");
        body.RootElement.GetProperty("ttl_sec").GetInt32().Should().Be(604800);
        body.RootElement.GetProperty("mark_email_as_verified").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("result_url").GetString().Should().Be(ManagerBaseUrl);
    }

    [Fact]
    public async Task CreatePasswordTicketAsync_ShouldNeverLogTheTicketUrl()
    {
        await CreateSut().CreatePasswordTicketAsync("auth0|u1");

        VerifyNeverLogged(TicketUrl);
        VerifyNeverLogged("s3cret");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Auth0ManagementProvisioner CreateSut()
    {
        var options = new Auth0ProvisionerOptions
        {
            Domain = Domain,
            ClientId = "client-id",
            ClientSecret = "client-secret",
        };

        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = options.BaseUri };

        return new Auth0ManagementProvisioner(
            httpClient,
            _tokenCache,
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new ManagerAppUrlOptions { BaseUrl = ManagerBaseUrl + "/" }),
            _time,
            _loggerMock.Object);
    }

    private static IdentityUserRequest OwnerRequest() => new(
        Email: "jay@nova.example.com",
        DisplayName: "Jay Lyons",
        TenantId: "ten_nova",
        OrgName: "Nova RV Services",
        LocationIds: [],
        Role: "dealer:owner");

    private static string ExistingUserJson(string userId, string? tenantId, string connection = "Username-Password-Authentication")
    {
        var metadata = tenantId is null ? "{}" : $$"""{"tenantId":"{{tenantId}}","orgName":"Existing"}""";
        return $$"""[{"user_id":"{{userId}}","email":"jay@nova.example.com","identities":[{"connection":"{{connection}}"}],"app_metadata":{{metadata}}}]""";
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private void VerifyNeverLogged(string fragment) =>
        _loggerMock.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(fragment)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body, string? Authorization)
    {
        public string Path => Uri.AbsolutePath;
    }

    /// <summary>Stands in for the Auth0 tenant: routes on method + path and records every request.</summary>
    private sealed class FakeAuth0Handler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public int TokenLifetimeSeconds { get; } = 3600;
        public string UsersByEmailJson { get; set; } = "[]";
        public string RolesJson { get; set; } =
            """[{"id":"rol_owner","name":"dealer:owner"},{"id":"rol_manager","name":"dealer:manager"},{"id":"rol_regional","name":"dealer:regional-manager"}]""";
        /// <summary>Roles returned for any user not in <see cref="UserRolesByUser"/>.</summary>
        public string UserRolesJson { get; set; } = "[]";
        public Dictionary<string, string> UserRolesByUser { get; } = [];

        /// <summary>User-search result pages, by <c>page</c> index; past the end is an empty page.</summary>
        public List<string> SearchPages { get; set; } = [];
        public HttpStatusCode DeleteUserStatus { get; set; } = HttpStatusCode.NoContent;
        public Func<HttpResponseMessage> CreateUserResponse { get; set; } = () => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = Json("""{"user_id":"auth0|new1","email":"jay@nova.example.com"}"""),
        };
        public Func<HttpResponseMessage> GetUserResponse { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""{"user_id":"auth0|u1","email":"sam@nova.example.com","app_metadata":{"tenantId":"ten_nova","orgName":"Nova RV Services"}}"""),
        };

        public RecordedRequest Single(HttpMethod method, string path) =>
            Requests.Should().ContainSingle(r => r.Method == method && r.Path == path).Which;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString());
            Requests.Add(recorded);

            var path = recorded.Path;
            var method = request.Method;

            if (method == HttpMethod.Post && path == "/oauth/token")
                return Ok($$"""{"access_token":"mgmt-token","expires_in":{{TokenLifetimeSeconds}},"token_type":"Bearer"}""");
            if (method == HttpMethod.Get && path == "/api/v2/users-by-email")
                return Ok(UsersByEmailJson);
            if (method == HttpMethod.Get && path == "/api/v2/roles")
                return Ok(RolesJson);
            if (method == HttpMethod.Post && path.StartsWith("/api/v2/roles/") && path.EndsWith("/users"))
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (method == HttpMethod.Post && path == "/api/v2/users")
                return CreateUserResponse();
            if (method == HttpMethod.Get && path == "/api/v2/users")
                return Ok(SearchPage(recorded.Uri));
            if (path.StartsWith("/api/v2/users/") && path.EndsWith("/roles"))
            {
                if (method == HttpMethod.Delete)
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                var userId = Uri.UnescapeDataString(path["/api/v2/users/".Length..^"/roles".Length]);
                return Ok(UserRolesByUser.GetValueOrDefault(userId, UserRolesJson));
            }
            if (method == HttpMethod.Delete && path.StartsWith("/api/v2/users/"))
                return new HttpResponseMessage(DeleteUserStatus);
            if (method == HttpMethod.Patch && path.StartsWith("/api/v2/users/"))
                return Ok($$$"""{"user_id":"{{{Uri.UnescapeDataString(path["/api/v2/users/".Length..])}}}","email":"sam@nova.example.com","app_metadata":{"tenantId":"ten_nova"}}""");
            if (method == HttpMethod.Get && path.StartsWith("/api/v2/users/"))
                return GetUserResponse();
            if (method == HttpMethod.Post && path == "/api/v2/tickets/password-change")
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = Json($$"""{"ticket":"{{TicketUrl}}"}""") };

            return new HttpResponseMessage(HttpStatusCode.NotImplemented);
        }

        private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = Json(json) };

        private string SearchPage(Uri uri)
        {
            var page = System.Web.HttpUtility.ParseQueryString(uri.Query)["page"];
            var index = int.TryParse(page, out var i) ? i : 0;
            return index < SearchPages.Count ? SearchPages[index] : "[]";
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
