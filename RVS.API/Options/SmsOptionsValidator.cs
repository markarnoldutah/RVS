using Microsoft.Extensions.Options;
using RVS.Domain.Validation;

namespace RVS.API.Options;

/// <summary>
/// Validates the bound <see cref="SmsOptions"/> (issue #661). Registered with
/// <c>ValidateOnStart</c>, so turning SMS on without a usable sending number stops the app at
/// startup instead of failing every send silently, which is how the hardcoded number failed.
/// </summary>
public sealed class SmsOptionsValidator : IValidateOptions<SmsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxMessagesPerTenantPerHour <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{SmsOptions.SectionName}:MaxMessagesPerTenantPerHour must be greater than zero.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.FromPhoneNumber))
        {
            return ValidateOptionsResult.Fail(
                $"{SmsOptions.SectionName}:FromPhoneNumber is required when SMS is enabled. In Azure it is "
                + "injected by Bicep (app-service-config.bicep) as AzureCommunicationServices__Sms__FromPhoneNumber.");
        }

        if (PhoneNumberNormalizer.Normalize(options.FromPhoneNumber) != options.FromPhoneNumber)
        {
            return ValidateOptionsResult.Fail(
                $"{SmsOptions.SectionName}:FromPhoneNumber must be a US/CA number in E.164 form, e.g. +18662319618.");
        }

        return ValidateOptionsResult.Success;
    }
}
