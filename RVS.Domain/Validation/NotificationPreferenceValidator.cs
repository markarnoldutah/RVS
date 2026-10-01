namespace RVS.Domain.Validation;

/// <summary>
/// Reconciles the customer's preferred contact method with their notification opt-outs
/// (<c>Spec A-2</c>, issues #577 and #662). The preference chooses the confirmation channel and
/// the opt-outs are a <b>hard veto</b> over it, so an opted-out channel can never be the
/// preference: <c>SmsOptOut</c> rules out <c>Text</c>, <c>EmailOptOut</c> rules out
/// <c>Email</c>, and <c>Phone</c> is always available.
///
/// The intake wizard and the intake API apply the same rule, so a hand-built request that pairs
/// <c>Text</c> with an SMS opt-out is rejected (422) rather than stored.
/// </summary>
public static class NotificationPreferenceValidator
{
    /// <summary>
    /// Whether <paramref name="method"/> may be chosen given the opt-outs. Matched
    /// case-insensitively with surrounding whitespace ignored. Values outside
    /// <see cref="PreferredContactMethod.AllowedValues"/> are not vetoed here.
    /// </summary>
    /// <param name="method">A preferred contact method, e.g. <c>Text</c>.</param>
    /// <param name="smsOptOut">The customer has opted out of text messages.</param>
    /// <param name="emailOptOut">The customer has opted out of email.</param>
    public static bool IsContactMethodAvailable(string? method, bool smsOptOut, bool emailOptOut)
    {
        var trimmed = method?.Trim();

        if (string.Equals(trimmed, PreferredContactMethod.Text, StringComparison.OrdinalIgnoreCase))
        {
            return !smsOptOut;
        }

        if (string.Equals(trimmed, PreferredContactMethod.Email, StringComparison.OrdinalIgnoreCase))
        {
            return !emailOptOut;
        }

        return true;
    }

    /// <summary>
    /// Fails when the preferred contact method is a channel the customer has opted out of.
    /// A blank preference is valid (it is stored as optional), and an unrecognised one is left
    /// to <see cref="PreferredContactMethod.Normalize"/>.
    /// </summary>
    /// <param name="preferredContact">The submitted preferred contact method.</param>
    /// <param name="smsOptOut">The customer has opted out of text messages.</param>
    /// <param name="emailOptOut">The customer has opted out of email.</param>
    public static ValidationResult Validate(string? preferredContact, bool smsOptOut, bool emailOptOut)
    {
        if (IsContactMethodAvailable(preferredContact, smsOptOut, emailOptOut))
        {
            return ValidationResult.Success;
        }

        var method = PreferredContactMethod.Normalize(preferredContact);
        var channel = method == PreferredContactMethod.Text ? "text messages" : "email";

        return ValidationResult.Failure(
            $"Preferred contact method '{method}' is not allowed because the customer has opted out of {channel}.");
    }
}
