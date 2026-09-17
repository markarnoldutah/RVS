using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.Infra.AzTableRepository;

/// <summary>
/// The fallback <see cref="IIntakeRedirectHitRepository"/> used when no Table Storage endpoint
/// is configured — the same pattern every other integration in the solution follows.
///
/// Hits are dropped, not buffered. The redirect still works, the channel still reaches the
/// resulting service request (which is the authoritative source-of-job record), and only the
/// conversion denominator is missing. That is the right trade for a developer machine with no
/// storage account, and it is why an unconfigured environment degrades quietly instead of
/// failing customers at the front door.
/// </summary>
public sealed class NoOpIntakeRedirectHitRepository : IIntakeRedirectHitRepository
{
    private readonly ILogger<NoOpIntakeRedirectHitRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NoOpIntakeRedirectHitRepository"/>.
    /// </summary>
    public NoOpIntakeRedirectHitRepository(ILogger<NoOpIntakeRedirectHitRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task AppendAsync(IntakeRedirectHit hit, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Intake redirect hits: no Table Storage endpoint configured; dropping hit for slug={Slug} src={Source}.",
            hit.Slug, hit.Source);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IntakeRedirectHit>> QueryAsync(
        string locationId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IntakeRedirectHit>>([]);
}
