namespace RVS.UI.Shared.Services;

/// <summary>
/// Lines Auth0's own session up with the manager app's "Keep me signed in on this device"
/// answer (issue #498 hardening).
///
/// The app's opt-in only controls whether its refresh token survives a restart. Auth0 keeps a
/// separate session cookie on its own domain, and the Blazor authentication library silently
/// signs in against that cookie on every auth-state check. Without this policy, a shared
/// computer whose last user closed the tab without signing out would sign the next person
/// straight into that account.
///
/// <para><b>Apply this per call, never globally.</b> The obvious-looking fix — adding
/// <c>prompt=login</c> to <c>OidcProviderOptions.AdditionalProviderParameters</c> once at
/// startup — was tried and reverted: that dictionary becomes the OIDC client's
/// <c>extraQueryParams</c>, which rides along on <i>every</i> authorize request the library
/// makes, including the automatic, invisible <c>signinSilent()</c> check it runs via a hidden
/// iframe on every auth-state read. <c>prompt=login</c> forces Auth0's interactive login
/// *form*, and Auth0 — like most IdPs — refuses to render that form inside a frame as an
/// anti-clickjacking measure, so the hidden iframe's navigation gets blocked: the browser
/// stalls for the iframe's timeout (observed ~3-5s) and then reports it as a failed request,
/// on every page load and right after every sign-out.</para>
///
/// <para>The safe alternative is to add <c>prompt=login</c> only to an explicit, interactive
/// sign-in request built with <c>InteractiveRequestOptions.TryAddAdditionalParameter</c> (see
/// <c>UnauthorizedAccess.razor</c> and <c>LoginDisplay.razor</c>). Such a request always goes
/// straight to a full-page <c>signinRedirect</c> and never through the silent/iframe path, so
/// it cannot repeat this failure.</para>
/// </summary>
public static class KeepSignedInPolicy
{
    /// <summary>The stored preference value that means the device opted in.</summary>
    public const string PreferenceYes = "yes";

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
}
