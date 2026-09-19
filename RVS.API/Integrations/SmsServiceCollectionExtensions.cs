using Azure.Communication.Sms;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Binds <see cref="SmsOptions"/>, registers the sender-number resolver and the tenant rate
    /// limiter, and picks the <see cref="ISmsNotificationService"/> implementation.
    ///
    /// <c>Sms:Enabled</c> is checked <b>before</b> the ACS endpoint. Both Key Vaults hold
    /// <c>AzureCommunicationServices--Endpoint</c> for email, so gating on the endpoint alone put
    /// the real ACS SMS service live in every environment before any number was verified.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration.</param>
    /// <param name="useMocks">The <c>Integrations:UseMocks</c> flag; forces the no-op service.</param>
    /// <param name="createSmsClient">
    /// Builds the ACS client for an endpoint. Only invoked when SMS is enabled and an endpoint is set.
    /// </param>
    public static IServiceCollection AddSmsNotifications(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useMocks,
        Func<Uri, SmsClient> createSmsClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(createSmsClient);

        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISmsSenderNumberResolver, ConfiguredSmsSenderNumberResolver>();
        services.AddSingleton<ITenantSmsRateLimiter, InMemoryTenantSmsRateLimiter>();

        var enabled = configuration.GetValue<bool>($"{SmsOptions.SectionName}:Enabled");
        var acsEndpoint = configuration["AzureCommunicationServices:Endpoint"];

        if (useMocks || !enabled || string.IsNullOrWhiteSpace(acsEndpoint))
        {
            services.AddSingleton<ISmsNotificationService, NoOpSmsNotificationService>();
            return services;
        }

        var acsUri = new Uri(acsEndpoint);
        services.AddSingleton(_ => createSmsClient(acsUri));
        services.AddScoped<ISmsNotificationService, AcsSmsNotificationService>();
        return services;
    }
}
