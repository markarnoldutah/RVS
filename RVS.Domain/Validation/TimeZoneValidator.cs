using System.Text.RegularExpressions;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates a location's IANA time-zone id (issue #506).
///
/// Blank is valid: an unset zone is the documented fallback, and the packet's Received line
/// stays in UTC. Anything else must be a zone this host can resolve — or one the product
/// curates, which is what keeps the picker's own zones saveable on a host whose ICU or tzdata
/// is missing. Without that short-circuit a globalization-invariant host would reject every
/// zone with a 400 and turn a cosmetic rendering feature into a broken settings screen.
/// </summary>
public static partial class TimeZoneValidator
{
    /// <summary>Maximum stored length. The longest real IANA id is well under this.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// IANA ids are drawn from letters, digits, <c>/</c>, <c>_</c>, <c>+</c> and <c>-</c>
    /// (<c>America/Indiana/Indianapolis</c>, <c>Etc/GMT+5</c>). Enforcing the alphabet per
    /// SEC-INPUT-02 means a hostile value can never reach the CSS <c>content:</c> string the
    /// running footer builds, independently of whether the zone resolves.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9/_+-]+$")]
    private static partial Regex TimeZoneIdPattern();

    /// <summary>Validates an optional IANA time-zone id.</summary>
    /// <param name="ianaTimeZoneId">The id to validate; <c>null</c> or blank is valid.</param>
    public static ValidationResult Validate(string? ianaTimeZoneId)
    {
        if (string.IsNullOrWhiteSpace(ianaTimeZoneId))
        {
            return ValidationResult.Success;
        }

        if (ianaTimeZoneId.Length > MaxLength)
        {
            return ValidationResult.Failure(
                $"Time zone id must not exceed {MaxLength} characters.");
        }

        if (!TimeZoneIdPattern().IsMatch(ianaTimeZoneId))
        {
            return ValidationResult.Failure(
                "Time zone id must contain only letters, digits, '/', '_', '+', and '-'.");
        }

        if (DealershipTimeZones.IsCurated(ianaTimeZoneId)
            || TimeZoneInfo.TryFindSystemTimeZoneById(ianaTimeZoneId, out _))
        {
            return ValidationResult.Success;
        }

        return ValidationResult.Failure(
            $"'{ianaTimeZoneId}' is not a recognised IANA time zone id.");
    }
}
