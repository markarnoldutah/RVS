using RVS.Domain.DTOs;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Resolves a <c>go.rvintake.com</c> short link to its intake URL and records the hit
/// (<c>Spec A-13</c>, issue #599). Every distribution path — QR sticker, texted link, printed
/// card — routes through here, so this is the one place a channel is observed.
/// </summary>
public interface IIntakeRedirectService
{
    /// <summary>
    /// Builds the redirect target for <paramref name="slug"/> and appends a hit record.
    /// Never throws on an unknown slug, an unknown <paramref name="src"/>, or a hit-log
    /// failure: the customer gets their redirect either way.
    /// </summary>
    /// <param name="slug">Location slug from the short link path.</param>
    /// <param name="src">Raw <c>src</c> query value, or <c>null</c> when absent (print).</param>
    /// <param name="userAgent">Client User-Agent, used only to flag machine fetches.</param>
    /// <param name="invite">
    /// Raw <c>inv</c> query value from an A-14 advisor invite link (issue #663), passed through
    /// to the intake URL. A malformed value is dropped, never an error.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IntakeRedirectResultDto> ResolveAsync(
        string slug,
        string? src,
        string? userAgent,
        string? invite = null,
        CancellationToken cancellationToken = default);
}
