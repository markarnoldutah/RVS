using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Per-location report of intake by distribution channel (<c>Spec A-13</c>, issue #599).
///
/// Channel data lives in two stores on purpose — submissions on the Cosmos service request,
/// raw redirect hits in append-only Table Storage — and a conversion rate needs both. This is
/// where they are joined: submissions are the numerator and the number anyone is shown, hits
/// are the denominator and nothing more.
///
/// Machine fetches are excluded from the reported hit counts. Messaging clients fetch a link to
/// build a preview the moment it is composed, so raw hits over-count opens by a factor that
/// varies with the client; <see cref="IntakeSourceReportResponseDto.TotalRawRedirectHits"/>
/// keeps the unfiltered number visible for diagnosis, but no dealer-facing surface should
/// present either hit count as "opens".
/// </summary>
public sealed class IntakeSourceReportService : IIntakeSourceReportService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IIntakeRedirectHitRepository _hitRepository;
    private readonly ILogger<IntakeSourceReportService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeSourceReportService"/>.
    /// </summary>
    public IntakeSourceReportService(
        IServiceRequestRepository serviceRequestRepository,
        IIntakeRedirectHitRepository hitRepository,
        ILogger<IntakeSourceReportService> logger)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _hitRepository = hitRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IntakeSourceReportResponseDto> GetForLocationAsync(
        string tenantId,
        string locationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        var submissions = await _serviceRequestRepository.GetForAnalyticsAsync(
            tenantId, fromUtc, toUtc, locationId, cancellationToken);

        var hits = await ReadHitsAsync(locationId, fromUtc, toUtc, cancellationToken);

        var submissionsBySource = submissions
            .GroupBy(r => IntakeSourceVocabulary.Normalize(r.IntakeSource))
            .ToDictionary(g => g.Key, g => g.Count());

        var hitsBySource = hits
            .GroupBy(h => IntakeSourceVocabulary.Normalize(h.Source))
            .ToDictionary(
                g => g.Key,
                g => (Raw: g.Count(), Human: g.Count(h => !h.IsLikelyBot)));

        var rows = submissionsBySource.Keys
            .Union(hitsBySource.Keys)
            .Select(source =>
            {
                var submissionCount = submissionsBySource.GetValueOrDefault(source);
                var (raw, human) = hitsBySource.GetValueOrDefault(source);

                return new IntakeSourceReportRowDto(
                    source,
                    IntakeSourceVocabulary.IsKnown(source),
                    submissionCount,
                    human,
                    raw,
                    ConversionRate(submissionCount, human));
            })
            .OrderByDescending(r => r.Submissions)
            .ThenByDescending(r => r.RedirectHits)
            .ThenBy(r => r.Source, StringComparer.Ordinal)
            .ToList();

        return new IntakeSourceReportResponseDto
        {
            LocationId = locationId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalSubmissions = submissions.Count,
            TotalRedirectHits = hits.Count(h => !h.IsLikelyBot),
            TotalRawRedirectHits = hits.Count,
            Rows = rows
        };
    }

    /// <summary>
    /// Reads the hit log, degrading to no hits when it is unavailable. The submissions half of
    /// this report is the authoritative record; a Table Storage outage costs the conversion
    /// denominator, not the whole report.
    /// </summary>
    private async Task<IReadOnlyList<IntakeRedirectHit>> ReadHitsAsync(
        string locationId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        try
        {
            return await _hitRepository.QueryAsync(locationId, fromUtc, toUtc, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Intake source report: redirect hit log unavailable for locationId={LocationId}; reporting submissions only.",
                locationId);
            return [];
        }
    }

    /// <summary>
    /// Submissions per non-bot redirect hit, to four places. <c>null</c> when the channel logged
    /// no non-bot hits — there is no rate to state, and zero would read as a failing channel.
    /// That is the normal state of <c>print</c>, whose links carry no <c>src</c>.
    /// </summary>
    private static double? ConversionRate(int submissions, int humanHits) =>
        humanHits == 0 ? null : Math.Round((double)submissions / humanHits, 4);
}
