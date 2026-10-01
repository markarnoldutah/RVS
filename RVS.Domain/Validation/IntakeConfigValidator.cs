using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates a location's or dealership's <see cref="IntakeFormConfigEmbedded"/> and resolves the
/// attachment cap the API and the intake app enforce (<c>Spec A-6</c>, issue #777).
/// </summary>
public static class IntakeConfigValidator
{
    /// <summary>
    /// Validates <paramref name="config"/> and returns the first problem found, or
    /// <see cref="ValidationResult.Success"/> when every rule passes.
    /// </summary>
    /// <param name="config">The intake configuration to validate. Must not be null.</param>
    public static ValidationResult Validate(IntakeFormConfigEmbedded config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.MaxAttachments < IntakeFormConfigEmbedded.MinAttachmentCap
            || config.MaxAttachments > IntakeFormConfigEmbedded.MaxAttachmentCap)
        {
            return ValidationResult.Failure(
                $"Maximum attachments must be between {IntakeFormConfigEmbedded.MinAttachmentCap} "
                + $"and {IntakeFormConfigEmbedded.MaxAttachmentCap}.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// The attachment cap to enforce for a configured value. A value above
    /// <see cref="IntakeFormConfigEmbedded.MaxAttachmentCap"/> — a record written before the cap
    /// dropped from 10 to 5 — is held to it; an absent or non-positive value falls back to it.
    /// </summary>
    /// <param name="configuredMaxAttachments">The stored <c>MaxAttachments</c>, or <c>null</c> when there is no config.</param>
    public static int EffectiveAttachmentCap(int? configuredMaxAttachments) =>
        configuredMaxAttachments is >= IntakeFormConfigEmbedded.MinAttachmentCap and var configured
            ? Math.Min(configured, IntakeFormConfigEmbedded.MaxAttachmentCap)
            : IntakeFormConfigEmbedded.MaxAttachmentCap;
}
