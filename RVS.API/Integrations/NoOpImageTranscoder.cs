using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Inert <see cref="IImageTranscoder"/> for the <c>Integrations:UseMocks</c> path: it claims
/// nothing and transcodes nothing, so every upload is stored exactly as received. HEIC
/// uploads then fall back to the packet's labelled placeholder, matching pre-<c>#508</c>
/// behaviour.
/// </summary>
public sealed class NoOpImageTranscoder : IImageTranscoder
{
    private readonly ILogger<NoOpImageTranscoder> _logger;

    /// <summary>Creates the no-op transcoder.</summary>
    public NoOpImageTranscoder(ILogger<NoOpImageTranscoder> logger) => _logger = logger;

    /// <inheritdoc />
    public bool CanTranscode(string? contentType) => false;

    /// <inheritdoc />
    public ImageTranscodeResult? TranscodeToJpeg(byte[] source, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOpImageTranscoder: would transcode {SourceBytes} bytes; keeping the original", source?.Length ?? 0);
        return null;
    }
}
