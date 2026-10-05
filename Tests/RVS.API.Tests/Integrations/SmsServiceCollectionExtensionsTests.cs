using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

/// <summary>
/// The SMS registration gate (issue #661): <c>Sms:Enabled</c> is checked before the Twilio
/// credentials, so an environment whose vault already holds them sends no SMS until its
/// toll-free number is verified and the flag is turned on.
/// </summary>
public class SmsServiceCollectionExtensionsTests
{
    private const string FromNumber = "+18885550100";

    private static readonly Dictionary<string, string?> TwilioCredentials = new()
    {
        ["Twilio:AccountSid"] = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        ["Twilio:ApiKeySid"] = "SKdddddddddddddddddddddddddddddddd",
        ["Twilio:ApiKeySecret"] = "secret",
    };

    [Fact]
    public void AddSmsNotifications_WhenDisabledWithTwilioCredentials_ShouldRegisterNoOp()
    {
        using var provider = Build(useMocks: false, With(TwilioCredentials, new()
        {
            ["Sms:Enabled"] = "false",
            ["Sms:FromPhoneNumber"] = FromNumber,
        }));

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
    }

    [Fact]
    public void AddSmsNotifications_WhenEnabledIsUnset_ShouldDefaultToNoOp()
    {
        using var provider = Build(useMocks: false, With(TwilioCredentials, new()));

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
    }

    [Fact]
    public void AddSmsNotifications_WhenEnabledWithTwilioCredentials_ShouldRegisterTwilio()
    {
        using var provider = Build(useMocks: false, With(TwilioCredentials, new()
        {
            ["Sms:Enabled"] = "true",
            ["Sms:FromPhoneNumber"] = FromNumber,
        }));

        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<TwilioSmsNotificationService>();
    }

    [Theory]
    [InlineData("Twilio:AccountSid")]
    [InlineData("Twilio:ApiKeySid")]
    [InlineData("Twilio:ApiKeySecret")]
    public void AddSmsNotifications_WhenEnabledButACredentialIsMissing_ShouldRegisterNoOp(string missing)
    {
        var settings = With(TwilioCredentials, new()
        {
            ["Sms:Enabled"] = "true",
            ["Sms:FromPhoneNumber"] = FromNumber,
        });
        settings.Remove(missing);

        using var provider = Build(useMocks: false, settings);

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
    }

    [Fact]
    public void AddSmsNotifications_WhenUsingMocks_ShouldRegisterNoOpEvenIfEnabled()
    {
        using var provider = Build(useMocks: true, With(TwilioCredentials, new()
        {
            ["Sms:Enabled"] = "true",
            ["Sms:FromPhoneNumber"] = FromNumber,
        }));

        provider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<NoOpSmsNotificationService>();
    }

    [Fact]
    public void AddSmsNotifications_ShouldBindSmsOptionsFromTheSmsSection()
    {
        using var provider = Build(useMocks: false, new()
        {
            ["Sms:Enabled"] = "true",
            ["Sms:FromPhoneNumber"] = FromNumber,
            ["Sms:MaxMessagesPerTenantPerHour"] = "42",
        });

        var options = provider.GetRequiredService<IOptions<SmsOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.FromPhoneNumber.Should().Be(FromNumber);
        options.MaxMessagesPerTenantPerHour.Should().Be(42);
    }

    [Fact]
    public void AddSmsNotifications_ShouldBindTwilioOptionsEvenWhenSmsIsOff()
    {
        // The inbound webhook verifies signatures with the auth token whether or not sending is on.
        using var provider = Build(useMocks: false, new()
        {
            ["Twilio:AuthToken"] = "token",
            ["Twilio:WebhookBaseUrl"] = "https://api.rvserviceflow.com",
        });

        var twilio = provider.GetRequiredService<IOptions<TwilioOptions>>().Value;

        twilio.AuthToken.Should().Be("token");
        twilio.WebhookBaseUrl.Should().Be("https://api.rvserviceflow.com");
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
            ["Sms:Enabled"] = "true",
        });

        var act = () => provider.GetRequiredService<IOptions<SmsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    private static Dictionary<string, string?> With(Dictionary<string, string?> first, Dictionary<string, string?> second) =>
        first.Concat(second).ToDictionary(kv => kv.Key, kv => kv.Value);

    private static ServiceProvider Build(bool useMocks, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSmsNotifications(configuration, useMocks);

        return services.BuildServiceProvider();
    }
}
