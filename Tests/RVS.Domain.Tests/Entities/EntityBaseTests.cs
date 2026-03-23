using FluentAssertions;
using RVS.Domain.Entities;

namespace RVS.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="EntityBase"/> audit fields and MarkAsUpdated behavior.
/// </summary>
public class EntityBaseTests
{
    /// <summary>
    /// Concrete test double for the abstract EntityBase.
    /// </summary>
    private sealed class TestEntity : EntityBase
    {
        public override string Type { get; init; } = "testEntity";
    }

    [Fact]
    public void NewEntity_ShouldHaveNonEmptyId()
    {
        var entity = new TestEntity();

        entity.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NewEntity_ShouldHaveNonEmptyTenantId()
    {
        var entity = new TestEntity();

        entity.TenantId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NewEntity_ShouldHaveCreatedAtUtcSetToApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity();
        var after = DateTime.UtcNow;

        entity.CreatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void NewEntity_ShouldHaveNullUpdatedAtUtc()
    {
        var entity = new TestEntity();

        entity.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void NewEntity_ShouldHaveNullUpdatedByUserId()
    {
        var entity = new TestEntity();

        entity.UpdatedByUserId.Should().BeNull();
    }

    [Fact]
    public void MarkAsUpdated_ShouldStampUpdatedAtUtcAndUpdatedByUserId()
    {
        var entity = new TestEntity();
        var userId = "user-123";

        entity.MarkAsUpdated(userId);

        entity.UpdatedAtUtc.Should().NotBeNull();
        entity.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.UpdatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void MarkAsUpdated_WithNullUserId_ShouldStillStampUpdatedAtUtc()
    {
        var entity = new TestEntity();

        entity.MarkAsUpdated();

        entity.UpdatedAtUtc.Should().NotBeNull();
        entity.UpdatedByUserId.Should().BeNull();
    }
}
