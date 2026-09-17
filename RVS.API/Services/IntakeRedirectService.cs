using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Links;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Resolves a <c>go.rvintake.com</c> short link to a location's intake URL and records the hit
/// (<c>Spec A-13</c>, issue #599).
///
/// Every distribution path routes through here — the QR sticker, the texted link, the printed
/// card — so this is the single place a channel can be observed, and the single place a change
/// to how links are handed out shows up. Two rules hold the implementation together:
///
/// 1. <b>The redirect always happens.</b> An unknown slug, a nonsense <c>src</c>, a Cosmos or
///    Table Storage failure — none of it is the customer's problem. A slug that does not
///    resolve still redirects, because the intake app owns the "no such location" page and
///    a 404 here would turn a typo on a sticker into a dead link.
/// 2. <b>Hits are recorded, not judged.</b> Machine fetches are flagged rather than dropped
///    (link previews fetch the URL before anybody taps it), and the store is append-only.
///    <see cref="ServiceRequest.IntakeSource"/> remains the authoritative source-of-job record.
/// </summary>
public sealed class IntakeRedirectService : IIntakeRedirectService
{
    private readonly ISlugLookupRepository _slugLookupRepository;
    private readonly IIntakeRedirectHitRepository _hitRepository;
    private readonly IntakeUrlOptions _intakeUrlOptions;
    private readonly ILogger<IntakeRedirectService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeRedirectService"/>.
    /// </summary>
    public IntakeRedirectService(
        ISlugLookupRepository slugLookupRepository,
        IIntakeRedirectHitRepository hitRepository,
        IOptions<IntakeUrlOptions> intakeUrlOptions,
        ILogger<IntakeRedirectService> logger)
    {
        _slugLookupRepository = slugLookupRepository;
        _hitRepository = hitRepository;
        _intakeUrlOptions = intakeUrlOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IntakeRedirectResultDto> ResolveAsync(
        string slug,
        string? src,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var source = IntakeSourceVocabulary.Normalize(src);

        // Resolution feeds the hit's partition key and nothing else — the redirect target is
        // built from the slug either way, so a lookup failure degrades to an unpartitioned hit.
        SlugLookup? slugLookup = null;
        try
        {
            slugLookup = await _slugLookupRepository.GetBySlugAsync(normalizedSlug, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Intake redirect: slug lookup failed for slug={Slug}; redirecting anyway.", normalizedSlug);
        }

        await RecordHitAsync(normalizedSlug, source, userAgent, slugLookup, cancellationToken);

        var targetUrl = IntakeLinkBuilder.IntakeUrl(_intakeUrlOptions.BaseUrl, normalizedSlug, source);

        _logger.LogInformation(
            "Intake redirect: slug={Slug} src={Source} resolved={Resolved} locationId={LocationId}",
            normalizedSlug, source, slugLookup is not null, slugLookup?.LocationId ?? "(none)");

        return new IntakeRedirectResultDto(targetUrl, source, slugLookup is not null);
    }

    /// <summary>
    /// Appends the hit, swallowing every failure. The append is awaited rather than
    /// fire-and-forget so a hit is not lost to the response completing first, but it is never
    /// allowed to surface: a lost hit costs a row in a conversion denominator, an exception here
    /// would cost the customer their intake form.
    /// </summary>
    private async Task RecordHitAsync(
        string slug,
        string source,
        string? userAgent,
        SlugLookup? slugLookup,
        CancellationToken cancellationToken)
    {
        try
        {
            var hit = new IntakeRedirectHit
            {
                LocationId = slugLookup?.LocationId ?? IntakeRedirectHit.UnresolvedLocationId,
                TenantId = slugLookup?.TenantId,
                Slug = slug,
                Source = source,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                IsLikelyBot = BotUserAgentFilter.IsLikelyBot(userAgent),
                UserAgent = Truncate(userAgent, IntakeRedirectHit.MaxUserAgentLength)
            };

            await _hitRepository.AppendAsync(hit, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Intake redirect: failed to record hit for slug={Slug} src={Source}; redirecting anyway.",
                slug, source);
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
