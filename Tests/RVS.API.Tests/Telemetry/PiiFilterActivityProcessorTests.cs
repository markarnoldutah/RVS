using System.Diagnostics;
using FluentAssertions;
using RVS.API.Telemetry;

namespace RVS.API.Tests.Telemetry;

public sealed class PiiFilterActivityProcessorTests
{
    private readonly PiiFilterActivityProcessor _processor = new();

    [Theory]
    [InlineData("email")]
    [InlineData("Email")]
    [InlineData("emailAddress")]
    [InlineData("EmailAddress")]
    [InlineData("phone")]
    [InlineData("Phone")]
    [InlineData("phoneNumber")]
    [InlineData("PhoneNumber")]
    [InlineData("firstName")]
    [InlineData("FirstName")]
    [InlineData("lastName")]
    [InlineData("LastName")]
    [InlineData("customerName")]
    [InlineData("CustomerName")]
    [InlineData("name")]
    [InlineData("Name")]
    public void OnEnd_ShouldRemovePiiTag(string piiKey)
    {
        using var activity = CreateActivityWithTag(piiKey, "sensitive-data");

        _processor.OnEnd(activity);

        activity.GetTagItem(piiKey).Should().BeNull();
    }

    [Fact]
    public void OnEnd_ShouldPreserveNonPiiTags()
    {
        using var activity = CreateActivityWithTag("TenantId", "t-42");
        activity.SetTag("CorrelationId", "corr-1");

        _processor.OnEnd(activity);

        activity.GetTagItem("TenantId").Should().Be("t-42");
        activity.GetTagItem("CorrelationId").Should().Be("corr-1");
    }

    [Fact]
    public void OnEnd_WhenNoPiiTagsPresent_ShouldNotThrow()
    {
        using var activity = CreateActivityWithTag("SafeTag", "value");

        var act = () => _processor.OnEnd(activity);

        act.Should().NotThrow();
        activity.GetTagItem("SafeTag").Should().Be("value");
    }

    [Fact]
    public void OnEnd_ShouldRemoveAllPiiTagsSimultaneously()
    {
        using var activity = CreateActivityWithTag("email", "user@example.com");
        activity.SetTag("phone", "555-1234");
        activity.SetTag("firstName", "John");
        activity.SetTag("TenantId", "keep-me");

        _processor.OnEnd(activity);

        activity.GetTagItem("email").Should().BeNull();
        activity.GetTagItem("phone").Should().BeNull();
        activity.GetTagItem("firstName").Should().BeNull();
        activity.GetTagItem("TenantId").Should().Be("keep-me");
    }

    [Fact]
    public void PiiTagKeys_ShouldCoverCommonPiiFields()
    {
        PiiFilterActivityProcessor.PiiTagKeys.Should().HaveCountGreaterThan(10);
        PiiFilterActivityProcessor.PiiTagKeys.Should().Contain("email");
        PiiFilterActivityProcessor.PiiTagKeys.Should().Contain("phone");
        PiiFilterActivityProcessor.PiiTagKeys.Should().Contain("firstName");
        PiiFilterActivityProcessor.PiiTagKeys.Should().Contain("lastName");
    }

    private static Activity CreateActivityWithTag(string key, string value)
    {
        var source = new ActivitySource("RVS.Tests.Pii");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        var activity = source.StartActivity("TestOperation")!;
        activity.SetTag(key, value);
        return activity;
    }
}
