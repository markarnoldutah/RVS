using FluentAssertions;
using Newtonsoft.Json.Linq;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="GlobalCustomerAcct"/> entity.
/// </summary>
public class GlobalCustomerAcctTests
{
    [Fact]
    public void NewGlobalCustomerAcct_TypeShouldBeGlobalCustomerAcct()
    {
        var acct = new GlobalCustomerAcct();

        acct.Type.Should().Be("globalCustomerAcct");
    }

    [Fact]
    public void NewGlobalCustomerAcct_ShouldHaveEmptyLinkedProfiles()
    {
        var acct = new GlobalCustomerAcct();

        acct.LinkedProfiles.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void NewGlobalCustomerAcct_ShouldHaveEmptyAllKnownAssetIds()
    {
        var acct = new GlobalCustomerAcct();

        acct.AllKnownAssetIds.Should().NotBeNull().And.BeEmpty();
    }

    // ── Opt-outs live on CustomerProfile only ────────────────────────────────
    // #673 stopped writing them here; the properties stayed and still serialized their defaults,
    // so every new account carried a second, never-read copy of the opt-out fields.

    [Theory]
    [InlineData("smsOptOut")]
    [InlineData("emailOptOut")]
    [InlineData("smsOptInAtUtc")]
    [InlineData("smsOptOutAtUtc")]
    [InlineData("emailOptOutAtUtc")]
    public void Serialize_ShouldNotWriteOptOutFields(string field)
    {
        var json = JObject.FromObject(new GlobalCustomerAcct { Email = "jane@example.com" });

        json.Properties().Select(p => p.Name).Should().NotContain(field);
    }

    // ── IdForEmail (issue #679) ──────────────────────────────────────────────
    // The container has no unique key, so the id is what makes a second create for the same
    // email collide instead of adding a duplicate account.

    [Fact]
    public void IdForEmail_WhenCalledTwiceWithTheSameEmail_ShouldReturnTheSameId()
    {
        GlobalCustomerAcct.IdForEmail("jane@example.com")
            .Should().Be(GlobalCustomerAcct.IdForEmail("jane@example.com"));
    }

    [Fact]
    public void IdForEmail_WhenEmailDiffersOnlyInCaseOrWhitespace_ShouldReturnTheSameId()
    {
        GlobalCustomerAcct.IdForEmail("  Jane@Example.COM ")
            .Should().Be(GlobalCustomerAcct.IdForEmail("jane@example.com"));
    }

    [Fact]
    public void IdForEmail_WhenEmailsDiffer_ShouldReturnDifferentIds()
    {
        GlobalCustomerAcct.IdForEmail("jane@example.com")
            .Should().NotBe(GlobalCustomerAcct.IdForEmail("john@example.com"));
    }

    [Theory]
    [InlineData("jane@example.com")]
    [InlineData("a/b?c#d\\e@example.com")]
    public void IdForEmail_ShouldBeAValidCosmosIdThatDoesNotContainTheEmail(string email)
    {
        var id = GlobalCustomerAcct.IdForEmail(email);

        id.Should().MatchRegex("^gca_[0-9a-f]{64}$");
        id.Should().NotContain("@");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void IdForEmail_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => GlobalCustomerAcct.IdForEmail(email!);

        act.Should().Throw<ArgumentException>();
    }
}
