using RVS.Domain.Validation;

namespace RVS.API.Packets;

/// <summary>
/// <see cref="ILocationLogoFetcher"/> over HTTP (<c>Spec A-16</c>, issue <c>#470</c>). The URL is
/// one a tenant's own manager set, and the bytes go only into that tenant's packet, but the
/// request still leaves from the API, so it is held tight: https only (and .NET's handler will not
/// follow a redirect from https down to http), at most <see cref="MaxLogoBytes"/> read, and only a
/// real PNG or JPEG accepted. Timeouts and retries come from the resilience handler registered
/// with the client. Any failure is a logged warning and a packet without a logo.
/// </summary>
public sealed class HttpLocationLogoFetcher : ILocationLogoFetcher
{
    /// <summary>Largest logo accepted, in bytes. A logo is a few tens of kilobytes.</summary>
    public const int MaxLogoBytes = 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpLocationLogoFetcher> _logger;

    /// <summary>Creates the fetcher over a client configured with a resilience handler.</summary>
    public HttpLocationLogoFetcher(HttpClient httpClient, ILogger<HttpLocationLogoFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]?> FetchAsync(string logoUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logoUrl);

        if (!LocationBrandingValidator.IsHttpsUrl(logoUrl))
        {
            _logger.LogWarning("Location logo skipped: {LogoUrl} is not an absolute https URL", logoUrl);
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                logoUrl.Trim(), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Location logo skipped: {LogoUrl} returned HTTP {StatusCode}", logoUrl, (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxLogoBytes)
            {
                _logger.LogWarning(
                    "Location logo skipped: {LogoUrl} is {Length} bytes, over the {MaxBytes}-byte cap",
                    logoUrl, response.Content.Headers.ContentLength, MaxLogoBytes);
                return null;
            }

            var bytes = await ReadCappedAsync(response.Content, cancellationToken);
            if (bytes is null)
            {
                _logger.LogWarning(
                    "Location logo skipped: {LogoUrl} is over the {MaxBytes}-byte cap", logoUrl, MaxLogoBytes);
                return null;
            }

            if (!PacketImageSignature.IsPngOrJpeg(bytes))
            {
                _logger.LogWarning("Location logo skipped: {LogoUrl} is not a PNG or JPEG image", logoUrl);
                return null;
            }

            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Location logo skipped: {LogoUrl} could not be fetched", logoUrl);
            return null;
        }
    }

    /// <summary>Reads the body, or returns <c>null</c> as soon as it passes <see cref="MaxLogoBytes"/>.</summary>
    private static async Task<byte[]?> ReadCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxLogoBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
