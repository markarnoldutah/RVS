using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.API.RateLimiting;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Registers the outbound SMS path (issue #661).
/// </summary>
public static class SmsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="SmsOptions"/> and <see cref="TwilioOptions"/>, registers the sender-number
    /// resolver and the tenant rate limiter, and picks the <see cref="ISmsNotificationService"/>
    /// implementation.
    ///
    /// <c>Sms:Enabled</c> is checked <b>before</b> the Twilio credentials. Each vault holds them
    /// from the day the subaccount exists, but a toll-free number cannot send until it clears
    /// verification, so the credentials alone must never put Twilio live.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration.</param>
    /// <param name="useMocks">The <c>Integrations:UseMocks</c> flag; forces the no-op service.</param>
    public static IServiceCollection AddSmsNotifications(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useMocks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>();

        // Bound whatever the switch says: the inbound webhook needs the auth token regardless.
        services.AddOptions<TwilioOptions>()
            .Bind(configuration.GetSection(TwilioOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISmsSenderNumberResolver, ConfiguredSmsSenderNumberResolver>();
        services.AddSingleton<ITenantSmsRateLimiter, InMemoryTenantSmsRateLimiter>();

        var enabled = configuration.GetValue<bool>($"{SmsOptions.SectionName}:Enabled");
        var twilio = configuration.GetSection(TwilioOptions.SectionName).Get<TwilioOptions>() ?? new TwilioOptions();

        if (useMocks || !enabled || !twilio.CanSend)
        {
            services.AddSingleton<ISmsNotificationService, NoOpSmsNotificationService>();
            return services;
        }

        services.AddHttpClient<ISmsNotificationService, TwilioSmsNotificationService>(client =>
            {
                client.BaseAddress = new Uri("https://api.twilio.com/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{twilio.ApiKeySid}:{twilio.ApiKeySecret}")));
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                // Never retry a send: a POST that timed out may still have been accepted, and a
                // retry would text the customer twice. A failed send is recorded, not repeated.
                options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
            });

        return services;
    }
}
