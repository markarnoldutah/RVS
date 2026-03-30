using Microsoft.JSInterop;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// No-op <see cref="IJSRuntime"/> for unit tests that don't exercise JS interop.
/// All invocations return default values silently.
/// </summary>
internal sealed class NullJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        default;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        default;
}
