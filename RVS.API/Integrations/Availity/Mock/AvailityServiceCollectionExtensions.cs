using RVS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RVS.API.Integrations.Availity.Mock;

/// <summary>
/// Extension methods for registering Availity services with optional mocking.
/// </summary>
public static class AvailityServiceCollectionExtensions
{
    /// <summary>
    /// Adds Availity eligibility client to the service collection.
    /// Uses mock client if MockAvailityOptions.UseMock is true in configuration.
    /// 
    /// Configuration:
    /// {
    ///   "Availity": {
    ///     "BaseUrl": "https://api.availity.com",
    ///     "EligibilityPath": "/availity/v1/coverages",
    ///     "ClientId": "...",
    ///     "ClientSecret": "..."
    ///   },
    ///   "AvailityMock": {
    ///     "UseMock": true,
    ///     "DefaultScenario": "Coverages-Complete-i",
    ///     "SimulatedDelayMs": 100
    ///   }
    /// }
    /// </summary>
    public static IServiceCollection AddAvailityEligibilityClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options
        var availityOptions = configuration.GetSection("Availity").Get<AvailityOptions>() 
            ?? new AvailityOptions();
        var mockOptions = configuration.GetSection("AvailityMock").Get<MockAvailityOptions>() 
            ?? new MockAvailityOptions();

        services.Configure<AvailityOptions>(configuration.GetSection("Availity"));
        services.Configure<MockAvailityOptions>(configuration.GetSection("AvailityMock"));

        if (mockOptions.UseMock)
        {
            // Register mock client
            services.AddScoped<IAvailityEligibilityClient>(sp =>
            {
                var logger = sp.GetService<ILogger<MockAvailityEligibilityClient>>();
                return new MockAvailityEligibilityClient(mockOptions, logger)
                {
                    Scenario = mockOptions.DefaultScenario,
                    SimulatedDelayMs = mockOptions.SimulatedDelayMs
                };
            });
        }
        else
        {
            // Register real client with HttpClientFactory
            services.AddHttpClient<IAvailityEligibilityClient, AvailityEligibilityClient>(client =>
            {
                client.BaseAddress = new Uri(availityOptions.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResilienceHandler(); // Polly retry, timeout, circuit breaker

            // Add auth handler if OAuth2 is configured
            if (!string.IsNullOrEmpty(availityOptions.ClientId))
            {
                services.AddTransient<AvailityAuthHandler>();
                services.ConfigureHttpClientDefaults(builder =>
                {
                    builder.AddHttpMessageHandler<AvailityAuthHandler>();
                });
            }
        }

        return services;
    }

    /// <summary>
    /// Adds Availity mock client directly (for unit tests).
    /// </summary>
    public static IServiceCollection AddMockAvailityEligibilityClient(
        this IServiceCollection services,
        Action<MockAvailityOptions>? configure = null)
    {
        var options = new MockAvailityOptions { UseMock = true };
        configure?.Invoke(options);

        services.AddScoped<IAvailityEligibilityClient>(sp =>
        {
            var logger = sp.GetService<ILogger<MockAvailityEligibilityClient>>();
            return new MockAvailityEligibilityClient(options, logger)
            {
                Scenario = options.DefaultScenario,
                SimulatedDelayMs = options.SimulatedDelayMs
            };
        });

        return services;
    }
}
