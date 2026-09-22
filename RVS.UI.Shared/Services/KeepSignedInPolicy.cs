namespace RVS.UI.Shared.Services;

/// <summary>
/// Lines Auth0's own session up with the manager app's "Keep me signed in on this device"
/// answer (issue #498 hardening).
///
/// The app's opt-in only controls whether its refresh token survives a restart. Auth0 keeps a
/// separate session cookie on its own domain, and the Blazor authentication library silently
/// signs in against that cookie on page load. Without this policy, a shared computer whose last
/// user closed the tab without signing out would sign the next person straight into that account.
///
/// Unless the device explicitly opted in, every authorize request carries <c>prompt=login</c>.
/// Auth0 then always asks for the password on an interactive sign-in, and it rejects the silent
/// sign-in outright (a request carrying both <c>prompt=none</c> and <c>prompt=login</c> fails
/// with <c>invalid_request</c>). Token renewal is unaffected: it uses the refresh token.
/// </summary>
public static class KeepSignedInPolicy
{
    /// <summary>The stored preference value that means the device opted in.</summary>
    public const string PreferenceYes = "yes";

    /// <summary>The stored preference value that means the device declined.</summary>
    public const string PreferenceNo = "no";

    /// <summary>
    /// The shared-computer warning shown wherever the device can opt in — the sign-in prompt and
    /// the profile menu — so both say the same thing.
    /// </summary>
    public const string SharedComputerWarning = "Don't choose this on a shared or public computer.";

    /// <summary>The OIDC authorize parameter that forces re-authentication.</summary>
    public const string PromptParameter = "prompt";

    /// <summary>The <see cref="PromptParameter"/> value that forces the password prompt.</summary>
    public const string PromptLoginValue = "login";

    /// <summary>
    /// Returns <c>true</c> unless <paramref name="preference"/> is exactly <see cref="PreferenceYes"/>.
    /// Unknown, blank, and differently-cased values fail closed.
    /// </summary>
    public static bool ShouldForceLogin(string? preference) =>
        !string.Equals(preference, PreferenceYes, StringComparison.Ordinal);

    /// <summary>
    /// Adds <c>prompt=login</c> to the provider's authorize parameters when login must be forced,
    /// and removes any <c>prompt</c> parameter otherwise. Other parameters are left untouched.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    public static void ApplyTo(IDictionary<string, string> parameters, string? preference)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (ShouldForceLogin(preference))
        {
            parameters[PromptParameter] = PromptLoginValue;
        }
        else
        {
            parameters.Remove(PromptParameter);
        }
    }

    /// <summary>
    /// Maps the stored preference to the state the profile menu shows. Only the exact stored
    /// answers count; anything else reads as <see cref="KeepSignedInState.NotSet"/>, matching
    /// <see cref="ShouldForceLogin"/>, which never treats such a value as opted in.
    /// </summary>
    public static KeepSignedInState ParseState(string? preference) => preference switch
    {
        PreferenceYes => KeepSignedInState.On,
        PreferenceNo => KeepSignedInState.Off,
        _ => KeepSignedInState.NotSet,
    };

    /// <summary>Returns the short label the profile menu shows for <paramref name="state"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not a defined value.</exception>
    public static string GetLabel(KeepSignedInState state) => state switch
    {
        KeepSignedInState.On => "On",
        KeepSignedInState.Off => "Off",
        KeepSignedInState.NotSet => "Not set",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown keep-signed-in state."),
    };

    /// <summary>
    /// Returns the argument for <c>rvsSession_setPersist</c> that stores <paramref name="state"/>.
    /// <see cref="KeepSignedInState.NotSet"/> is not an answer — it is reached by resetting.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not On or Off.</exception>
    public static bool ToPersist(KeepSignedInState state) => state switch
    {
        KeepSignedInState.On => true,
        KeepSignedInState.Off => false,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Only On or Off can be stored; reset clears the answer."),
    };
}
