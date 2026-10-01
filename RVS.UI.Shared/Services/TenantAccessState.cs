namespace RVS.UI.Shared.Services;

/// <summary>
/// Whether the signed-in user's tenant has been disabled, as learned from the API's
/// "tenant disabled" 403 (issue #625). Set by <see cref="TenantAccessGateHandler"/>; the manager
/// layout replaces every page with an "access restricted" message while it is set.
/// Register as a singleton — WASM has one user per tab.
/// </summary>
public sealed class TenantAccessState
{
    /// <summary><c>true</c> once any API call has reported the tenant disabled.</summary>
    public bool IsRestricted { get; private set; }

    /// <summary>The tenant's own message for its users, if one was set.</summary>
    public string? DisabledMessage { get; private set; }

    /// <summary>Who the user should contact, if one was set.</summary>
    public string? SupportContactEmail { get; private set; }

    /// <summary>Raised when <see cref="IsRestricted"/> changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Records that the tenant is disabled. Raises <see cref="Changed"/> only on the first call,
    /// because every page fires several API calls and each one gets the same 403.
    /// </summary>
    public void MarkRestricted(string? disabledMessage, string? supportContactEmail)
    {
        if (IsRestricted)
        {
            return;
        }

        IsRestricted = true;
        DisabledMessage = string.IsNullOrWhiteSpace(disabledMessage) ? null : disabledMessage.Trim();
        SupportContactEmail = string.IsNullOrWhiteSpace(supportContactEmail) ? null : supportContactEmail.Trim();
        Changed?.Invoke();
    }

    /// <summary>Clears the restriction, e.g. before retrying after logins were re-enabled.</summary>
    public void Reset()
    {
        IsRestricted = false;
        DisabledMessage = null;
        SupportContactEmail = null;
        Changed?.Invoke();
    }
}
