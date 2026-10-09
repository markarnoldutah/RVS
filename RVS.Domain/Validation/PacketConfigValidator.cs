using System.ComponentModel.DataAnnotations;
using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates a location's <see cref="PacketConfigEmbedded"/> (<c>Spec B-6</c> / <c>C-6</c>):
/// the 0–<see cref="PacketConfigEmbedded.MaxRecipients"/> recipient bound and address shape,
/// the paste-block cap range (<c>Spec B-5</c>), and the status-link TTL bound (<c>Spec X-5</c>).
/// The location logo is branding, not packet configuration: see <see cref="LocationBrandingValidator"/>.
/// </summary>
public static class PacketConfigValidator
{
    /// <summary>Longest e-mail address accepted for a recipient (RFC 5321).</summary>
    private const int MaxRecipientLength = 254;

    private static readonly EmailAddressAttribute EmailValidator = new();

    /// <summary>
    /// Validates <paramref name="config"/> and returns the first problem found, or
    /// <see cref="ValidationResult.Success"/> when every rule passes.
    /// </summary>
    /// <param name="config">The packet configuration to validate. Must not be null.</param>
    public static ValidationResult Validate(PacketConfigEmbedded config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Recipients is null)
        {
            return ValidationResult.Failure("Packet recipient list must not be null.");
        }

        if (config.Recipients.Count > PacketConfigEmbedded.MaxRecipients)
        {
            return ValidationResult.Failure(
                $"A location may have at most {PacketConfigEmbedded.MaxRecipients} packet recipients.");
        }

        foreach (var recipient in config.Recipients)
        {
            var recipientResult = ValidateRecipientAddress(recipient);
            if (!recipientResult.IsValid)
            {
                return recipientResult;
            }
        }

        if (config.PasteBlockCharacterCap < PacketConfigEmbedded.MinPasteBlockCharacterCap
            || config.PasteBlockCharacterCap > PacketConfigEmbedded.MaxPasteBlockCharacterCap)
        {
            return ValidationResult.Failure(
                $"Paste-block character cap must be between {PacketConfigEmbedded.MinPasteBlockCharacterCap} "
                + $"and {PacketConfigEmbedded.MaxPasteBlockCharacterCap}.");
        }

        if (config.StatusLinkTtlDays < 1
            || config.StatusLinkTtlDays > PacketConfigEmbedded.MaxStatusLinkTtlDays)
        {
            return ValidationResult.Failure(
                $"Status-link TTL must be between 1 and {PacketConfigEmbedded.MaxStatusLinkTtlDays} days.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates one active packet-recipient address: not blank, at most 254 characters, no
    /// whitespace, and a well-formed email address. Exposed so the manager settings form can
    /// check an address as it is typed (issue #447) against the same rule the API enforces.
    /// </summary>
    /// <param name="recipient">The address to check.</param>
    public static ValidationResult ValidateRecipientAddress(string? recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return ValidationResult.Failure("Packet recipient addresses must not be blank.");
        }

        if (recipient.Length > MaxRecipientLength)
        {
            return ValidationResult.Failure(
                $"Packet recipient address must not exceed {MaxRecipientLength} characters.");
        }

        if (recipient.Any(char.IsWhiteSpace) || !EmailValidator.IsValid(recipient))
        {
            return ValidationResult.Failure($"'{recipient}' is not a valid email address.");
        }

        return ValidationResult.Success;
    }
}
