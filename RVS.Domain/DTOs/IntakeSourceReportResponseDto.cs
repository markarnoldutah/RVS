namespace RVS.Domain.DTOs;

/// <summary>
/// Per-location breakdown of intake by distribution channel (<c>Spec A-13</c>, issue #599),
/// joining the two stores the channel data lives in: submissions come from the service
/// requests, redirect hits from the append-only <c>go.rvintake.com</c> hit log.
///
/// <see cref="IntakeSourceReportRowDto.Submissions"/> is the metric to show a dealer.
/// Redirect hits are reported alongside it only so a conversion rate has a denominator — they
/// include machine fetches and must never be presented to a customer as "opens".
/// </summary>
public sealed record IntakeSourceReportResponseDto
{
    /// <summary>The location this report covers.</summary>
    public required string LocationId { get; init; }

    /// <summary>Inclusive start of the reporting window (UTC), or <c>null</c> for all time.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Exclusive end of the reporting window (UTC), or <c>null</c> for open-ended.</summary>
    public DateTime? ToUtc { get; init; }

    /// <summary>Submissions in the window, all channels.</summary>
    public int TotalSubmissions { get; init; }

    /// <summary>Redirect hits in the window with obvious machine fetches excluded, all channels.</summary>
    public int TotalRedirectHits { get; init; }

    /// <summary>
    /// Every redirect hit in the window, machine fetches included. Diagnostic only — the gap
    /// between this and <see cref="TotalRedirectHits"/> is what link-preview fetching costs.
    /// </summary>
    public int TotalRawRedirectHits { get; init; }

    /// <summary>One row per channel seen in the window, submissions descending.</summary>
    public List<IntakeSourceReportRowDto> Rows { get; init; } = [];
}

/// <summary>
/// One channel's row in an <see cref="IntakeSourceReportResponseDto"/>.
/// </summary>
/// <param name="Source">Channel tag — see <c>RVS.Domain.Validation.IntakeSourceVocabulary</c>.</param>
/// <param name="IsKnownSource">Whether the tag is one of the tabled channels rather than an ad-hoc one.</param>
/// <param name="Submissions">Service requests attributed to this channel.</param>
/// <param name="RedirectHits">Redirect hits for this channel, machine fetches excluded.</param>
/// <param name="RawRedirectHits">Redirect hits for this channel including machine fetches.</param>
/// <param name="ConversionRate">
/// <paramref name="Submissions"/> ÷ <paramref name="RedirectHits"/>, rounded to four places.
/// <c>null</c> when the channel logged no non-bot hits — which is the normal state of
/// <c>print</c>, whose links reach the redirect without ever carrying a <c>src</c>.
/// </param>
public sealed record IntakeSourceReportRowDto(
    string Source,
    bool IsKnownSource,
    int Submissions,
    int RedirectHits,
    int RawRedirectHits,
    double? ConversionRate);
