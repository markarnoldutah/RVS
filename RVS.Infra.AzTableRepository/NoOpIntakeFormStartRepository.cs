using Microsoft.Extensions.Logging;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.Infra.AzTableRepository;

/// <summary>
/// The fallback <see cref="IIntakeFormStartRepository"/> used when no Table Storage endpoint is
/// configured, mirroring <see cref="NoOpIntakeRedirectHitRepository"/>. Starts are dropped; the
/// form is unaffected and only the completion-rate denominator is missing.
/// </summary>
public sealed class NoOpIntakeFormStartRepository : IIntakeFormStartRepository
{
    private readonly ILogger<NoOpIntakeFormStartRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NoOpIntakeFormStartRepository"/>.
    /// </summary>
    public NoOpIntakeFormStartRepository(ILogger<NoOpIntakeFormStartRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task AppendAsync(IntakeFormStart start, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Intake form starts: no Table Storage endpoint configured; dropping start for slug={Slug} src={Source}.",
            start.Slug, start.Source);

        return Task.CompletedTask;
    }
}
