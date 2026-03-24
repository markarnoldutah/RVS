using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Middleware;

namespace RVS.API.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock = new();
    private readonly Mock<IWebHostEnvironment> _envMock = new();
    private readonly ExceptionHandlingMiddleware _middleware;

    public ExceptionHandlingMiddlewareTests()
    {
        _envMock.Setup(e => e.EnvironmentName).Returns("Production");
        _middleware = new ExceptionHandlingMiddleware(_loggerMock.Object, _envMock.Object);
    }

    [Fact]
    public async Task ArgumentException_Returns400_WithProblemDetails()
    {
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new ArgumentException("Invalid parameter");

        await _middleware.InvokeAsync(context, next);

        var problem = await DeserializeProblemDetails(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Contain("application/problem+json");
        problem.Type.Should().Be("https://api.rvserviceflow.com/errors/bad-request");
        problem.Title.Should().Be("Bad Request");
        problem.Status.Should().Be(400);
        problem.Detail.Should().Be("Invalid parameter");
        problem.Instance.Should().Be("/api/test");
    }

    [Fact]
    public async Task UnauthorizedAccessException_Returns401_WithProblemDetails()
    {
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new UnauthorizedAccessException("Tenant identifier is missing.");

        await _middleware.InvokeAsync(context, next);

        var problem = await DeserializeProblemDetails(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Type.Should().Be("https://api.rvserviceflow.com/errors/unauthorized");
        problem.Title.Should().Be("Unauthorized");
        problem.Status.Should().Be(401);
        problem.Detail.Should().Be("Tenant identifier is missing.");
        problem.Instance.Should().Be("/api/test");
    }

    [Fact]
    public async Task KeyNotFoundException_Returns404_WithProblemDetails()
    {
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new KeyNotFoundException("Service request not found.");

        await _middleware.InvokeAsync(context, next);

        var problem = await DeserializeProblemDetails(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.Type.Should().Be("https://api.rvserviceflow.com/errors/not-found");
        problem.Title.Should().Be("Resource not found");
        problem.Status.Should().Be(404);
        problem.Detail.Should().Be("Service request not found.");
        problem.Instance.Should().Be("/api/test");
    }

    [Fact]
    public async Task UnhandledException_Returns500_WithProblemDetails()
    {
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new InvalidOperationException("Something went wrong");

        await _middleware.InvokeAsync(context, next);

        var problem = await DeserializeProblemDetails(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Type.Should().Be("https://api.rvserviceflow.com/errors/internal-server-error");
        problem.Title.Should().Be("Internal Server Error");
        problem.Status.Should().Be(500);
        problem.Detail.Should().Be("Something went wrong");
        problem.Instance.Should().Be("/api/test");
    }

    [Fact]
    public async Task ProblemDetails_ContainsErrorId()
    {
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new Exception("Test");

        await _middleware.InvokeAsync(context, next);

        var body = await ReadResponseBody(context);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("errorId", out var errorIdProp).Should().BeTrue();
        errorIdProp.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Development_IncludesExceptionAndStackTrace()
    {
        _envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var devMiddleware = new ExceptionHandlingMiddleware(_loggerMock.Object, _envMock.Object);

        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new ArgumentException("Bad input");

        await devMiddleware.InvokeAsync(context, next);

        var body = await ReadResponseBody(context);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("exception", out var exProp).Should().BeTrue();
        exProp.GetString().Should().Be("ArgumentException");
        doc.RootElement.TryGetProperty("stackTrace", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Production_DoesNotIncludeExceptionOrStackTrace()
    {
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new ArgumentException("Bad input");

        await _middleware.InvokeAsync(context, next);

        var body = await ReadResponseBody(context);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("exception", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("stackTrace", out _).Should().BeFalse();
    }

    [Fact]
    public async Task NoException_PassesThrough()
    {
        var context = CreateHttpContext();
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        await _middleware.InvokeAsync(context, next);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static async Task<ProblemDetails> DeserializeProblemDetails(HttpContext context)
    {
        var body = await ReadResponseBody(context);
        return JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize ProblemDetails");
    }
}
