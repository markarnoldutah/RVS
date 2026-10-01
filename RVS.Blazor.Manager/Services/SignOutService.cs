using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using RVS.UI.Shared.Services;

namespace RVS.Blazor.Manager.Services;

/// <summary>
/// Signs the manager out: used by the profile menu and by the "access restricted" screen
/// (issue #625), so both leave the device in the same state.
/// </summary>
public sealed class SignOutService(
    IJSRuntime js,
    IConfiguration configuration,
    RefreshTokenRevocationClient revocationClient,
    NavigationManager navigation)
{
    public async Task SignOutAsync()
    {
        // Revoke the refresh token at Auth0 and drop the persisted sign-in before the framework's
        // own sign-out (issue #498), so a token copied earlier stops working. Best effort: a
        // failed or slow revoke never blocks signing out — RevokeAsync is bounded by a timeout.
        try
        {
            var refreshToken = await js.InvokeAsync<string?>("rvsAuth_getRefreshToken", configuration["Auth0:ClientId"]);
            await revocationClient.RevokeAsync(refreshToken);
            await js.InvokeVoidAsync("rvsSession_clearPersisted");
        }
        catch (JSException)
        {
            // Interop unavailable — the framework sign-out below still removes the user record,
            // which also clears the mirror.
        }

        navigation.NavigateToLogout("authentication/logout");
    }
}
