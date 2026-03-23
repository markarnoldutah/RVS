using FluentAssertions;
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
    public void NewGlobalCustomerAcct_ShouldHaveEmptyAllKnownVins()
    {
        var acct = new GlobalCustomerAcct();

        acct.AllKnownVins.Should().NotBeNull().And.BeEmpty();
    }
}
