using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.Domain.Entities;
using RVS.Infra.AzTableRepository;

namespace RVS.API.Tests.Repositories;

/// <summary>
/// Tests for <see cref="AzTableIntakeFormStartRepository"/> (<c>Spec A-13</c>, issue #839). The
/// Azure SDK clients are classes, so they are faked by subclassing rather than mocked.
/// </summary>
public class AzTableIntakeFormStartRepositoryTests
{
    private readonly FakeTableClient _table = new();
    private readonly AzTableIntakeFormStartRepository _sut;

    public AzTableIntakeFormStartRepositoryTests()
    {
        _sut = new AzTableIntakeFormStartRepository(
            new FakeTableServiceClient(_table),
            Mock.Of<ILogger<AzTableIntakeFormStartRepository>>());
    }

    [Fact]
    public void Constructor_ShouldUseTheIntakeFormStartsTable()
    {
        var serviceClient = new FakeTableServiceClient(_table);

        _ = new AzTableIntakeFormStartRepository(serviceClient, Mock.Of<ILogger<AzTableIntakeFormStartRepository>>());

        serviceClient.RequestedTableName.Should().Be("intakeFormStarts");
    }

    [Fact]
    public void Constructor_WhenTableServiceClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new AzTableIntakeFormStartRepository(null!, Mock.Of<ILogger<AzTableIntakeFormStartRepository>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_WhenStartIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.AppendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_ShouldWriteOneRowPartitionedByLocation()
    {
        await _sut.AppendAsync(BuildStart());

        _table.Added.Should().ContainSingle();
        _table.Added[0].PartitionKey.Should().Be("loc_hurricane");
        _table.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task AppendAsync_ShouldKeyNewerStartsBeforeOlderOnes()
    {
        var now = DateTimeOffset.UtcNow;

        await _sut.AppendAsync(BuildStart(now.AddMinutes(-5)));
        await _sut.AppendAsync(BuildStart(now));

        // Table Storage only sorts ascending, so the newer start must have the smaller key.
        string.CompareOrdinal(_table.Added[1].RowKey, _table.Added[0].RowKey).Should().BeNegative();
    }

    [Fact]
    public async Task AppendAsync_WhenTwoStartsShareATick_ShouldGiveThemDistinctKeys()
    {
        var at = DateTimeOffset.UtcNow;

        await _sut.AppendAsync(BuildStart(at));
        await _sut.AppendAsync(BuildStart(at));

        _table.Added[0].RowKey.Should().NotBe(_table.Added[1].RowKey);
    }

    [Fact]
    public async Task AppendAsync_WhenTableIsMissing_ShouldCreateItAndRetryOnce()
    {
        _table.FailNextAddWith = (int)HttpStatusCode.NotFound;

        await _sut.AppendAsync(BuildStart());

        _table.CreateCalls.Should().Be(1);
        _table.Added.Should().ContainSingle();
    }

    [Fact]
    public async Task AppendAsync_WhenAddFailsForAnotherReason_ShouldThrowWithoutCreating()
    {
        // The service swallows this; the repository only owns the create-on-404 case.
        _table.FailNextAddWith = (int)HttpStatusCode.Forbidden;

        var act = () => _sut.AppendAsync(BuildStart());

        await act.Should().ThrowAsync<RequestFailedException>();
        _table.CreateCalls.Should().Be(0);
    }

    private static IntakeFormStart BuildStart(DateTimeOffset? at = null) => new()
    {
        LocationId = "loc_hurricane",
        TenantId = "ten_nova_rv",
        Slug = "nova-hurricane",
        Source = "qr",
        SessionId = "3f2b8c0e9d4a4f6b8e1c2d3a4b5c6d7e",
        OccurredAtUtc = at ?? DateTimeOffset.UtcNow,
        IsLikelyBot = false,
        UserAgent = "Mozilla/5.0"
    };

    private sealed class FakeTableServiceClient(TableClient table) : TableServiceClient
    {
        public string? RequestedTableName { get; private set; }

        public override TableClient GetTableClient(string tableName)
        {
            RequestedTableName = tableName;
            return table;
        }
    }

    private sealed class FakeTableClient : TableClient
    {
        public List<ITableEntity> Added { get; } = [];

        public int CreateCalls { get; private set; }

        public int? FailNextAddWith { get; set; }

        public override Task<Response> AddEntityAsync<T>(T entity, CancellationToken cancellationToken = default)
        {
            if (FailNextAddWith is { } status)
            {
                FailNextAddWith = null;
                throw new RequestFailedException(status, "simulated");
            }

            Added.Add(entity);
            return Task.FromResult<Response>(null!);
        }

        public override Task<Response<TableItem>> CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult<Response<TableItem>>(null!);
        }
    }
}
