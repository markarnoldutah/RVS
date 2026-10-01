using Microsoft.JSInterop;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// <see cref="IJSRuntime"/> that records the identifier of every invocation and returns the
/// default value. Set <see cref="ThrowOnInvoke"/> to simulate a JS-side failure.
/// </summary>
internal sealed class RecordingJSRuntime : IJSRuntime
{
    public List<string> Invocations { get; } = [];

    public Exception? ThrowOnInvoke { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Invocations.Add(identifier);
        return ThrowOnInvoke is null ? default : ValueTask.FromException<TValue>(ThrowOnInvoke);
    }
}
