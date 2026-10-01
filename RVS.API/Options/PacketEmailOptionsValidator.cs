using Microsoft.Extensions.Options;
using RVS.Domain.Validation;

namespace RVS.API.Options;

/// <summary>
/// Runs <see cref="PacketEmailBudgetValidator"/> against the bound <see cref="PacketEmailOptions"/>
/// (issue #521). Registered with <c>ValidateOnStart</c> in <c>Program.cs</c>, so an out-of-range
/// <c>PacketEmail:MaxRequestBytes</c> stops the app at startup instead of showing up per packet.
/// The rules live in the Domain validator; this only adapts its result to the options pipeline.
/// </summary>
public sealed class PacketEmailOptionsValidator : IValidateOptions<PacketEmailOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PacketEmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = PacketEmailBudgetValidator.Validate(options.MaxRequestBytes);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.ErrorMessage!);
    }
}
