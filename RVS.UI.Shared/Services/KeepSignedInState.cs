namespace RVS.UI.Shared.Services;

/// <summary>
/// This device's answer to "Keep me signed in on this device?", as shown in the manager app's
/// profile menu (issue #616).
/// </summary>
public enum KeepSignedInState
{
    /// <summary>Not asked yet — the prompt appears on the next sign-in.</summary>
    NotSet,

    /// <summary>The device opted in: the sign-in survives restarts and Auth0 may skip the password.</summary>
    On,

    /// <summary>The device declined: the session ends with the tab and sign-in asks for the password.</summary>
    Off,
}
