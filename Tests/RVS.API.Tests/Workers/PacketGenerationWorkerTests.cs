using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Workers;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;

namespace RVS.API.Tests.Workers;

/// <summary>
/// Tests for <see cref="PacketGenerationWorker"/> — the background consumer that drains
/// <see cref="IPacketGenerationQueue"/> and runs one <see cref="IPacketGenerationService"/>
/// attempt per job in its own DI scope (issue #434).
/// </summary>
public class PacketGenerationWorkerTests
{
    private readonly Mock<IPacketGenerationService> _serviceMock = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public PacketGenerationWorkerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_serviceMock.Object);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private PacketGenerationWorker BuildWorker(IPacketGenerationQueue queue) =>
        new(queue, _scopeFactory, Mock.Of<ILogger<PacketGenerationWorker>>());

    // ── ProcessOnceAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessOnceAsync_ShouldResolveTheServiceInAScopeAndDelegate()
    {
        _serviceMock.Setup(s => s.GenerateAsync("ten_1", "sr_9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PacketGenerationOutcome.Succeeded);
        var worker = BuildWorker(Mock.Of<IPacketGenerationQueue>());

        var outcome = await worker.ProcessOnceAsync(new PacketGenerationJob("ten_1", "sr_9", "intake"), CancellationToken.None);

        outcome.Should().Be(PacketGenerationOutcome.Succeeded);
        _serviceMock.Verify(s => s.GenerateAsync("ten_1", "sr_9", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOnceAsync_WhenServiceThrowsUnexpectedly_ShouldSwallowAndReturnNull()
    {
        _serviceMock.Setup(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cosmos unavailable"));
        var worker = BuildWorker(Mock.Of<IPacketGenerationQueue>());

        var outcome = await worker.ProcessOnceAsync(new PacketGenerationJob("ten_1", "sr_9", "intake"), CancellationToken.None);

        outcome.Should().BeNull();
    }

    // ── ExecuteAsync drains the queue ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldProcessEveryQueuedJobUntilTheQueueCompletes()
    {
        _serviceMock.Setup(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PacketGenerationOutcome.Succeeded);
        var queue = new CompletableQueue();
        queue.Enqueue(new PacketGenerationJob("ten_1", "sr_1", "intake"));
        queue.Enqueue(new PacketGenerationJob("ten_1", "sr_2", "regeneration"));
        queue.Complete();

        var worker = BuildWorker(queue);
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        _serviceMock.Verify(s => s.GenerateAsync("ten_1", "sr_1", It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(s => s.GenerateAsync("ten_1", "sr_2", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An <see cref="IPacketGenerationQueue"/> whose stream ends when <see cref="Complete"/> is called.</summary>
    private sealed class CompletableQueue : IPacketGenerationQueue
    {
        private readonly Channel<PacketGenerationJob> _channel = Channel.CreateUnbounded<PacketGenerationJob>();

        public void Enqueue(PacketGenerationJob job) => _channel.Writer.TryWrite(job);
        public void Complete() => _channel.Writer.Complete();

        public bool TryEnqueue(PacketGenerationJob job) => _channel.Writer.TryWrite(job);

        public IAsyncEnumerable<PacketGenerationJob> DequeueAllAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
