using RVS.Domain.Interfaces;
using RVS.Domain.Packets;

namespace RVS.API.Workers;

/// <summary>
/// Background consumer for <see cref="IPacketGenerationQueue"/> (issue #434). Drains queued
/// <see cref="PacketGenerationJob"/>s, runs one <see cref="IPacketGenerationService.GenerateAsync"/>
/// attempt each in its own DI scope, and re-queues after a short delay while attempts remain
/// (<see cref="Entities.PacketGenerationEmbedded.MaxAttempts"/>). Generation failures are handled
/// inside the service; only an unexpected fault reaches this loop, and it is logged and dropped
/// rather than re-queued, so one poison job cannot spin.
/// </summary>
public sealed class PacketGenerationWorker : BackgroundService
{
    /// <summary>Delay before re-queuing a failed-but-retryable job. Keeps three attempts inside roughly the <c>Spec B-1</c> P95 window without hammering a failing dependency.</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IPacketGenerationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PacketGenerationWorker> _logger;

    public PacketGenerationWorker(
        IPacketGenerationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<PacketGenerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PacketGenerationWorker started");

        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            var outcome = await ProcessOnceAsync(job, stoppingToken);

            if (outcome == PacketGenerationOutcome.Retry)
            {
                ScheduleRetry(job, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Runs one generation attempt for <paramref name="job"/> in its own DI scope. Returns the
    /// service outcome, or <c>null</c> when an unexpected fault was caught (logged, not re-queued).
    /// </summary>
    internal async Task<PacketGenerationOutcome?> ProcessOnceAsync(PacketGenerationJob job, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPacketGenerationService>();

            return await service.GenerateAsync(job.TenantId, job.ServiceRequestId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PacketGenerationWorker: unhandled error generating packet for SR {ServiceRequestId} (trigger {Trigger}); not re-queued",
                job.ServiceRequestId, job.Trigger);
            return null;
        }
    }

    /// <summary>
    /// Re-queues <paramref name="job"/> after <see cref="RetryDelay"/> on a detached task so the
    /// worker keeps draining other jobs meanwhile.
    /// </summary>
    private void ScheduleRetry(PacketGenerationJob job, CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
                if (!_queue.TryEnqueue(job))
                {
                    _logger.LogWarning(
                        "PacketGenerationWorker: could not re-queue retry for SR {ServiceRequestId}; it stays Failed until a manual regenerate",
                        job.ServiceRequestId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down.
            }
        }, stoppingToken);
    }
}
