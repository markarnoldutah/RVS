using System.Collections.Concurrent;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Integration;

/// <summary>
/// In-memory <see cref="IIntakeFormStartRepository"/> for the form-start integration tests
/// (<c>Spec A-13</c>, issue #839), so rows can be asserted on without a storage account.
/// </summary>
public sealed class FakeIntakeFormStartRepository : IIntakeFormStartRepository
{
    private readonly ConcurrentQueue<IntakeFormStart> _starts = new();

    /// <summary>Everything appended so far, oldest first.</summary>
    public IReadOnlyList<IntakeFormStart> Starts => [.. _starts];

    public Task AppendAsync(IntakeFormStart start, CancellationToken cancellationToken = default)
    {
        _starts.Enqueue(start);
        return Task.CompletedTask;
    }
}
