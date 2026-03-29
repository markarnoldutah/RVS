using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RVS.Domain.Exceptions;

namespace RVS.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns an RFC 9457 ProblemDetails response.
/// Registered as a singleton <see cref="IMiddleware"/>.
/// </summary>
public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    private const string ErrorBaseUri = "https://api.rvserviceflow.com/errors";
    private const string TenantIdClaimType = "https://rvserviceflow.com/tenantId";

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorId = Guid.NewGuid();
        var tenantId = context.User?.FindFirst(TenantIdClaimType)?.Value;
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Map known exception types → status codes + RFC 9457 fields
        var (statusCode, title, errorType) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "validation"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not-found"),
            MagicLinkExpiredException => (StatusCodes.Status410Gone, "Gone", "token-expired"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "internal-server-error")
        };

        _logger.LogError(exception,
            "Unhandled exception. errorId={ErrorId}, statusCode={StatusCode}, tenantId={TenantId}, userId={UserId}, path={Path}",
            errorId,
            statusCode,
            tenantId,
            userId,
            context.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Type = $"{ErrorBaseUri}/{errorType}",
            Title = title,
            Status = statusCode,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["errorId"] = errorId;

        if (_env.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problemDetails,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
