using RVS.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RVS.API.Integrations.Availity.Mock;

/// <summary>
/// Middleware that allows setting mock scenarios via HTTP header.
/// Supports Availity's X-Api-Mock-Scenario-ID pattern for testing.
/// 
/// Usage:
/// Send request with header: X-Api-Mock-Scenario-ID: Coverages-Complete-i
/// 
/// Only active when UseMock is true in configuration.
/// </summary>
public sealed class MockScenarioMiddleware
{
    private readonly RequestDelegate _next;
    private const string ScenarioHeader = "X-Api-Mock-Scenario-ID";
    private const string MockResponseHeader = "X-Api-Mock-Response";

    public MockScenarioMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for scenario header
        if (context.Request.Headers.TryGetValue(ScenarioHeader, out var scenarioId) &&
            !string.IsNullOrWhiteSpace(scenarioId))
        {
            // Get the mock client from DI and set scenario
            var availityClient = context.RequestServices.GetService<IAvailityEligibilityClient>();
            if (availityClient is MockAvailityEligibilityClient mockClient)
            {
                mockClient.Scenario = scenarioId.ToString();

                // Add response header to confirm mock was used
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[MockResponseHeader] = "true";
                    return Task.CompletedTask;
                });
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for mock middleware registration.
/// </summary>
public static class MockMiddlewareExtensions
{
    /// <summary>
    /// Adds middleware that allows setting mock scenarios via X-Api-Mock-Scenario-ID header.
    /// Should only be used in development/test environments.
    /// </summary>
    public static IApplicationBuilder UseMockScenarioMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MockScenarioMiddleware>();
    }
}
