namespace RVS.Domain.Interfaces;

/// <summary>
/// Records that a visit reached the first screen of a location's intake form (<c>Spec A-13</c>,
/// issue #839), so completion rate per location and per channel can be computed.
/// </summary>
public interface IIntakeFormStartService
{
    /// <summary>
    /// Appends a start for <paramref name="slug"/>. Never throws for an unknown or A-19-expired
    /// slug, a malformed <paramref name="sessionId"/>, or a storage failure: those cost the row,
    /// not the form. Unknown and expired slugs, and malformed session ids, write nothing.
    /// </summary>
    /// <param name="slug">Location slug from the intake URL.</param>
    /// <param name="sessionId">The intake app's per-tab visit id.</param>
    /// <param name="src">Raw <c>src</c> the visit arrived with, or <c>null</c> for print.</param>
    /// <param name="userAgent">Client User-Agent, used only to flag crawlers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(
        string slug,
        string? sessionId,
        string? src,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
