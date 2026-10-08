using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Records a visit reaching Step 1 of a location's intake form (<c>Spec A-13</c>, issue #839) —
/// the denominator of the completion rate. It follows the A-13 redirect-hit pattern
/// (<see cref="IntakeRedirectService"/>): append-only Table Storage, crawlers flagged rather
/// than dropped, and nothing here is ever allowed to reach the customer. The intake app does not
/// wait on the call and never retries it.
///
/// Unlike a redirect hit, a start is only recorded for a slug that resolves and has not expired
/// (<c>Spec A-19</c>): there is no form to start otherwise, and a row in an "unresolved"
/// partition would be a start nobody can divide by.
/// </summary>
public sealed class IntakeFormStartService : IIntakeFormStartService
{
    private readonly ISlugLookupRepository _slugLookupRepository;
    private readonly ITenantConfigRepository _tenantConfigRepository;
    private readonly IIntakeFormStartRepository _startRepository;
    private readonly ILogger<IntakeFormStartService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeFormStartService"/>.
    /// </summary>
    public IntakeFormStartService(
        ISlugLookupRepository slugLookupRepository,
        ITenantConfigRepository tenantConfigRepository,
        IIntakeFormStartRepository startRepository,
        ILogger<IntakeFormStartService> logger)
    {
        _slugLookupRepository = slugLookupRepository;
        _tenantConfigRepository = tenantConfigRepository;
        _startRepository = startRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        string slug,
        string? sessionId,
        string? src,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        if (!IntakeFormStart.IsWellFormedSessionId(sessionId))
        {
            _logger.LogDebug("Intake form start: malformed session id for slug={Slug}; not recorded.", normalizedSlug);
            return;
        }

        try
        {
            var slugLookup = await _slugLookupRepository.GetBySlugAsync(normalizedSlug, cancellationToken);
            if (slugLookup is null)
            {
                _logger.LogDebug("Intake form start: unknown slug={Slug}; not recorded.", normalizedSlug);
                return;
            }

            if (await IsIntakeExpiredAsync(slugLookup.TenantId, cancellationToken))
            {
                _logger.LogDebug("Intake form start: slug={Slug} has expired (Spec A-19); not recorded.", normalizedSlug);
                return;
            }

            var start = new IntakeFormStart
            {
                LocationId = slugLookup.LocationId,
                TenantId = slugLookup.TenantId,
                Slug = normalizedSlug,
                Source = IntakeSourceVocabulary.Normalize(src),
                SessionId = sessionId!,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                IsLikelyBot = BotUserAgentFilter.IsLikelyBot(userAgent),
                UserAgent = Truncate(userAgent, IntakeRedirectHit.MaxUserAgentLength)
            };

            await _startRepository.AppendAsync(start, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Intake form start: failed to record a start for slug={Slug}; the form is unaffected.",
                normalizedSlug);
        }
    }

    /// <summary>
    /// Whether the slug's tenant is past the A-19 capture window. A failed read counts as not
    /// expired: the worst case is a start recorded for a notice page, which beats losing a real one.
    /// </summary>
    private async Task<bool> IsIntakeExpiredAsync(string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var tenantConfig = await _tenantConfigRepository.GetAsync(tenantId, cancellationToken);
            return tenantConfig?.AccessGate?.IsIntakeExpired(DateTimeOffset.UtcNow) ?? false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Intake form start: tenant config read failed for tenantId={TenantId}; recording the start anyway.",
                tenantId);
            return false;
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
