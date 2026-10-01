namespace RVS.API.Packets;

/// <summary>
/// Fetches a location's dealer logo (<c>Spec A-16</c>, issue <c>#470</c>) so the packet PDF can
/// embed it. The packet generation orchestrator calls this once per generation, and passes the
/// logo on to the HTML only when this returns bytes, so both renderings show it or neither does.
/// </summary>
public interface ILocationLogoFetcher
{
    /// <summary>
    /// Returns the logo's bytes when <paramref name="logoUrl"/> is https and serves a PNG or JPEG
    /// within the size cap; <c>null</c> for anything else. Never throws for a bad logo — only for
    /// a blank URL or the caller's own cancellation.
    /// </summary>
    Task<byte[]?> FetchAsync(string logoUrl, CancellationToken cancellationToken = default);
}
