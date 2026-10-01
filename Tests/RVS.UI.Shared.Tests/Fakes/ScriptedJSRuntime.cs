using Microsoft.JSInterop;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// <see cref="IJSRuntime"/> that records every call and answers from <see cref="Handlers"/>,
/// keyed by JS identifier. A handler that returns an <see cref="Exception"/> makes the call fail
/// with it; an identifier with no handler returns the default value.
/// </summary>
internal sealed class ScriptedJSRuntime : IJSRuntime
{
    public List<(string Identifier, object?[] Args)> Calls { get; } = [];

    public Dictionary<string, Func<object?[], object?>> Handlers { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Calls.Add((identifier, args ?? []));

        if (!Handlers.TryGetValue(identifier, out var handler))
        {
            return default;
        }

        return handler(args ?? []) switch
        {
            Exception ex => ValueTask.FromException<TValue>(ex),
            null => default,
            var value => ValueTask.FromResult((TValue)value)
        };
    }
}
