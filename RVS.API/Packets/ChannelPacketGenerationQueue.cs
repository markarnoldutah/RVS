using System.Threading.Channels;
using RVS.Domain.Interfaces;
using RVS.Domain.Packets;

namespace RVS.API.Packets;

/// <summary>
/// In-process <see cref="IPacketGenerationQueue"/> backed by a bounded
/// <see cref="Channel{T}"/> (issue #434). Registered as a singleton; drained by
/// <see cref="Workers.PacketGenerationWorker"/>.
///
/// <para><b>Durability.</b> Jobs live only in memory, so a job enqueued but not yet rendered is
/// lost if the process restarts. That is bounded by design: every request persists its
/// <c>Pending</c> packet-generation state, the manager app surfaces it, and an on-demand
/// regenerate re-queues it. Swapping in a durable transport (e.g. Azure Storage Queue) is a new
/// implementation of this interface plus a DI change — no caller touches the channel.</para>
/// </summary>
public sealed class ChannelPacketGenerationQueue : IPacketGenerationQueue
{
    /// <summary>
    /// Upper bound on queued jobs. Comfortably above realistic burst volume
    /// (fair-use is ~300 requests/location/month); a full queue drops the write and the caller
    /// falls back to the persisted <c>Pending</c> state.
    /// </summary>
    private const int Capacity = 1000;

    private readonly Channel<PacketGenerationJob> _channel = Channel.CreateBounded<PacketGenerationJob>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    /// <inheritdoc />
    public bool TryEnqueue(PacketGenerationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return _channel.Writer.TryWrite(job);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<PacketGenerationJob> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
