using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.UI.Shared.Validation;

/// <summary>
/// Client-side checks for the per-location packet settings form (<c>Spec B-6</c> / <c>C-6</c>,
/// issue #447). Address shape, ranges and the logo URL delegate to the domain
/// <see cref="PacketConfigValidator"/>, so the form and the API agree.
///
/// One rule is stricter here than on the server: while packet email is enabled, a save needs
/// at least one recipient. The API still accepts an enabled location with none, because that
/// is what a newly created location defaults to (<c>Spec B-6</c>) — the settings form is where
/// a manager is asked to finish the job.
/// </summary>
public static class ClientPacketConfigValidator
{
    /// <summary>
    /// Validates an address about to be added to the recipient list: well-formed, not already
    /// present (case-insensitive), and the list not already full.
    /// </summary>
    /// <param name="address">The address as typed; surrounding whitespace is ignored.</param>
    /// <param name="currentRecipients">The recipients already on the form.</param>
    public static ValidationResult ValidateNewRecipient(string? address, IReadOnlyCollection<string> currentRecipients)
    {
        ArgumentNullException.ThrowIfNull(currentRecipients);

        var trimmed = address?.Trim();
        var shape = PacketConfigValidator.ValidateRecipientAddress(trimmed);
        if (!shape.IsValid)
        {
            return shape;
        }

        if (currentRecipients.Any(r => string.Equals(r.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidationResult.Failure($"'{trimmed}' is already a recipient.");
        }

        if (currentRecipients.Count >= PacketConfigEmbedded.MaxRecipients)
        {
            return ValidationResult.Failure(
                $"A location may have at most {PacketConfigEmbedded.MaxRecipients} packet recipients.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates the whole packet configuration before it is saved: 1–10 distinct recipients
    /// while enabled (0–10 while disabled), then every other B-6 rule the API enforces.
    /// </summary>
    /// <param name="config">The configuration the form is about to send.</param>
    public static ValidationResult ValidateForSave(PacketConfigDto config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var recipients = config.Recipients ?? [];

        if (config.Enabled && recipients.Count == 0)
        {
            return ValidationResult.Failure(
                "Add at least one recipient, or turn packet email off for this location.");
        }

        var duplicate = recipients
            .GroupBy(r => r?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return ValidationResult.Failure($"'{duplicate.Key}' is listed more than once.");
        }

        return PacketConfigValidator.Validate(new PacketConfigEmbedded
        {
            Enabled = config.Enabled,
            Recipients = [.. recipients.Select(r => r?.Trim() ?? string.Empty)],
            AttachPdf = config.AttachPdf,
            IncludePhotos = config.IncludePhotos,
            PasteBlockCharacterCap = config.PasteBlockCharacterCap,
            StatusLinkTtlDays = config.StatusLinkTtlDays,
            LogoUrl = config.LogoUrl?.Trim(),
        });
    }
}
