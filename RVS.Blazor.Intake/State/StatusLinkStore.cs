using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace RVS.Blazor.Intake.State;

/// <summary>
/// Remembers the customer's status-page token on this device so "Check Request Status" can open
/// it directly instead of asking for the confirmation number (issue #716).
/// <para>
/// Persisted in <c>localStorage</c>, not <c>sessionStorage</c>: the point is that it survives the
/// tab being closed. It is stored alongside the expiry the API reported, and a link past that
/// expiry is treated as absent and forgotten. Storage that is blocked or unreadable is also
/// treated as absent — remembering the link is a convenience, never a reason for the page to fail.
/// </para>
/// </summary>
public sealed class StatusLinkStore
{
    /// <summary>The <c>localStorage</c> key the link is kept under.</summary>
    public const string StorageKey = "rvs_intake_status_link";

    private readonly IJSRuntime _jsRuntime;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="StatusLinkStore"/>.
    /// </summary>
    public StatusLinkStore(IJSRuntime jsRuntime, TimeProvider timeProvider)
    {
        _jsRuntime = jsRuntime;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Remembers <paramref name="token"/>, replacing any link already saved. A blank token, or one
    /// already past <paramref name="expiresAtUtc"/>, is ignored. A <c>null</c> expiry means the
    /// token does not expire, which is how the API treats it too.
    /// </summary>
    public async Task SaveAsync(string? token, DateTime? expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(token) || IsExpired(expiresAtUtc))
        {
            return;
        }

        var json = JsonSerializer.Serialize(new SavedStatusLink(token, expiresAtUtc));
        await TryAsync(() => _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json).AsTask());
    }

    /// <summary>
    /// Returns the saved token when there is one and it has not expired; otherwise <c>null</c>.
    /// An expired or unreadable entry is removed on the way out.
    /// </summary>
    public async Task<string?> GetUnexpiredTokenAsync()
    {
        string? json = null;
        await TryAsync(async () => json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey));

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        SavedStatusLink? link;
        try
        {
            link = JsonSerializer.Deserialize<SavedStatusLink>(json);
        }
        catch (JsonException)
        {
            link = null;
        }

        if (link is null || string.IsNullOrWhiteSpace(link.Token) || IsExpired(link.ExpiresAtUtc))
        {
            await RemoveAsync();
            return null;
        }

        return link.Token;
    }

    /// <summary>
    /// Forgets the saved link when it is <paramref name="token"/> — called when the API says that
    /// token is unknown or expired. A different saved link is left alone, so a dead link opened
    /// from an old email does not wipe a good one.
    /// </summary>
    public async Task ForgetIfSavedAsync(string token)
    {
        string? json = null;
        await TryAsync(async () => json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey));

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var link = JsonSerializer.Deserialize<SavedStatusLink>(json);
            if (link is not null && !string.Equals(link.Token, token, StringComparison.Ordinal))
            {
                return;
            }
        }
        catch (JsonException)
        {
            // Unreadable — forget it as well.
        }

        await RemoveAsync();
    }

    private bool IsExpired(DateTime? expiresAtUtc) =>
        expiresAtUtc.HasValue &&
        AsUtc(expiresAtUtc.Value) <= _timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private Task RemoveAsync() =>
        TryAsync(() => _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey).AsTask());

    private static async Task TryAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (JSException)
        {
            // localStorage blocked (private window, site data disabled) — behave as if empty.
        }
    }

    private sealed record SavedStatusLink(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("expiresAtUtc")] DateTime? ExpiresAtUtc);
}
