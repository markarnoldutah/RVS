using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock that returns a hardcoded transcript with high confidence for any audio input.
/// </summary>
public sealed class MockSpeechToTextService : ISpeechToTextService
{
    private const string MockTranscript = "My water heater stopped working yesterday and now there is no hot water in the RV.";
    private const string MockCleanedDescription = "Water heater stopped working. No hot water available in the RV.";
    private const double MockConfidence = 0.92;

    private readonly ILogger<MockSpeechToTextService> _logger;

    public MockSpeechToTextService(ILogger<MockSpeechToTextService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SpeechToTextResult?> TranscribeAudioAsync(byte[] audioData, string contentType, string locale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioData);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        _logger.LogDebug("MockSpeechToTextService returning hardcoded transcript for audio ({Bytes} bytes, {ContentType}, {Locale})",
            audioData.Length, contentType, locale);

        var result = new SpeechToTextResult(MockTranscript, MockCleanedDescription, MockConfidence, nameof(MockSpeechToTextService));
        return Task.FromResult<SpeechToTextResult?>(result);
    }
}
