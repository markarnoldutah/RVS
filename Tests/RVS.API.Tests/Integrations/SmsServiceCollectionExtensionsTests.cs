using Azure.Communication.Sms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// The SMS registration gate (issue #661): <c>Sms:Enabled</c> is checked before the ACS endpoint,
/// so an environment whose vault holds <c>AzureCommunicationServices--Endpoint</c> still sends no
/// SMS until the flag is turned on.
/// </summary>
public class SmsServiceCollectionExtensionsTests
{
    private const string AcsEndpoint = "https://rvs-acs-test.communication.azure.com";
    private const string FromNumber = "+18662319618";

    private int _smsClientsCreated;

    [Fact]
    public void AddSmsNotifications_WhenDisabledWithAnAcsEndpoint_ShouldRegisterNoOpAndNeverBuildAnSmsClient()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["AzureCommunicationServices:Endpoint"] = AcsEndpoint,
            ["AzureCommunicationServices:Sms:Enabled"] = "false",
            ["AzureCommunicationServices:Sms:FromPhoneNumber"] = FromNumber,
        });

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
        provider.GetService<SmsClient>().Should().BeNull();
        _smsClientsCreated.Should().Be(0);
    }

    [Fact]
    public void AddSmsNotifications_WhenEnabledIsUnset_ShouldDefaultToNoOp()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["AzureCommunicationServices:Endpoint"] = AcsEndpoint,
        });

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
        _smsClientsCreated.Should().Be(0);
    }

    [Fact]
    public void AddSmsNotifications_WhenEnabledWithAnAcsEndpoint_ShouldRegisterAcs()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["AzureCommunicationServices:Endpoint"] = AcsEndpoint,
            ["AzureCommunicationServices:Sms:Enabled"] = "true",
            ["AzureCommunicationServices:Sms:FromPhoneNumber"] = FromNumber,
        });

        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<AcsSmsNotificationService>();
        _smsClientsCreated.Should().Be(1);
    }

    [Fact]
    public void AddSmsNotifications_WhenEnabledWithoutAnAcsEndpoint_ShouldRegisterNoOp()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["AzureCommunicationServices:Sms:Enabled"] = "true",
            ["AzureCommunicationServices:Sms:FromPhoneNumber"] = FromNumber,
        });

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
        _smsClientsCreated.Should().Be(0);
    }

    [Fact]
    public void AddSmsNotifications_WhenUsingMocks_ShouldRegisterNoOpEvenIfEnabled()
    {
        using var provider = Build(useMocks: true, new()
        {
            ["AzureCommunicationServices:Endpoint"] = AcsEndpoint,
            ["AzureCommunicationServices:Sms:Enabled"] = "true",
            ["AzureCommunicationServices:Sms:FromPhoneNumber"] = FromNumber,
        });

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
        _smsClientsCreated.Should().Be(0);
    }

    [Fact]
    public void AddSmsNotifications_ShouldBindSmsOptionsFromTheAcsSmsSection()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["AzureCommunicationServices:Sms:Enabled"] = "true",
            ["AzureCommunicationServices:Sms:FromPhoneNumber"] = FromNumber,
            ["AzureCommunicationServices:Sms:MaxMessagesPerTenantPerHour"] = "42",
        });

        var options = provider.GetRequiredService<IOptions<SmsOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.FromPhoneNumber.Should().Be(FromNumber);
        options.MaxMessagesPerTenantPerHour.Should().Be(42);
    }

    [Fact]
    public void AddSmsNotifications_ShouldRegisterTheSenderResolverAndTenantRateLimiter()
    {
        using var provider = Build(useMocks: false, new());

        provider.GetRequiredService<ISmsSenderNumberResolver>().Should().BeOfType<ConfiguredSmsSenderNumberResolver>();
        provider.GetRequiredService<ITenantSmsRateLimiter>().Should()
            .BeSameAs(provider.GetRequiredService<ITenantSmsRateLimiter>(), "the hourly count must be shared across requests");
    }

    [Fact]
    public void AddSmsNotifications_WhenEnabledWithoutAFromNumber_ShouldFailOptionsValidation()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["AzureCommunicationServices:Sms:Enabled"] = "true",
        });

        var act = () => provider.GetRequiredService<IOptions<SmsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    private ServiceProvider Build(bool useMocks, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSmsNotifications(configuration, useMocks, endpoint =>
        {
            _smsClientsCreated++;
            return new SmsClient($"endpoint={endpoint};accesskey=dGVzdA==");
        });

        return services.BuildServiceProvider();
    }
}
