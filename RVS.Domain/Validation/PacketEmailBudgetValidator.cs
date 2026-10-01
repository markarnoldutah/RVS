using System.Globalization;
using RVS.Domain.Packets;

namespace RVS.Domain.Validation;

/// <summary>
/// Validates the packet-email size budget, <c>PacketEmail:MaxRequestBytes</c> (<c>Spec B-4</c>,
/// issue #521). It runs once at startup, so a bad value stops the app instead of quietly
/// degrading every packet email.
///
/// <para><b>Lower bound.</b> The budget must leave room for the packet PDF, which the fitter
/// always considers first. QuestPDF resamples embedded images to their placed size, so the PDF
/// does not grow with source resolution — but it does grow with photo count and detail. Measured
/// with real 12 MP phone photos it is 1.60 MB for six and 2.83 MB for ten, the most
/// <c>Spec A-6</c> allows, which is about 3.8 MB once base64 encoded. A budget below
/// <see cref="MinimumMaxRequestBytes"/> could not reliably hold it.</para>
///
/// <para><b>Upper bound.</b> ACS rejects any request over
/// <see cref="PacketEmailSizeFitter.AcsMaxRequestBytes"/>. A budget above that would let the
/// fitter pass a set ACS then refuses, bringing back the original #521 failure: every attempt
/// rejected, no packet delivered. If ACS approves a larger limit, change that constant
/// deliberately rather than raising this setting past it.</para>
/// </summary>
public static class PacketEmailBudgetValidator
{
    /// <summary>
    /// Smallest budget accepted: 5 MB. Clears the largest measured packet PDF on the wire — ten
    /// photos, about 3.8 MB — with room for both message bodies and for photos more detailed
    /// than the ones measured.
    /// </summary>
    public const long MinimumMaxRequestBytes = 5_000_000;

    /// <summary>The configuration key an operator has to fix, named in every failure.</summary>
    private const string SettingName = "PacketEmail:MaxRequestBytes";

    /// <summary>
    /// Validates <paramref name="maxRequestBytes"/> and returns the problem found, or
    /// <see cref="ValidationResult.Success"/> when it is within both bounds (inclusive).
    /// </summary>
    /// <param name="maxRequestBytes">The configured total request budget, base64 included.</param>
    public static ValidationResult Validate(long maxRequestBytes)
    {
        if (maxRequestBytes < MinimumMaxRequestBytes)
        {
            return ValidationResult.Failure(string.Create(
                CultureInfo.InvariantCulture,
                $"{SettingName} is {maxRequestBytes:N0} bytes; it must be at least {MinimumMaxRequestBytes:N0} so the packet PDF always fits."));
        }

        if (maxRequestBytes > PacketEmailSizeFitter.AcsMaxRequestBytes)
        {
            return ValidationResult.Failure(string.Create(
                CultureInfo.InvariantCulture,
                $"{SettingName} is {maxRequestBytes:N0} bytes; it must not exceed ACS's {PacketEmailSizeFitter.AcsMaxRequestBytes:N0}-byte request ceiling, above which every packet email is rejected."));
        }

        return ValidationResult.Success;
    }
}
