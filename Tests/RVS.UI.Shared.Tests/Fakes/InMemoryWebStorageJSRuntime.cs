using Microsoft.JSInterop;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// <see cref="IJSRuntime"/> that backs <c>getItem</c>, <c>setItem</c> and <c>removeItem</c> on one
/// Web Storage area (<c>sessionStorage</c> by default, or <c>localStorage</c>) with a dictionary, so
/// persistence can be round-tripped in a unit test. Every other call returns the default value.
/// </summary>
internal sealed class InMemoryWebStorageJSRuntime(string storage = "sessionStorage") : IJSRuntime
{
    /// <summary>The stored items, exposed so a test can seed or inspect them directly.</summary>
    public Dictionary<string, string> Items { get; } = [];

    /// <summary>
    /// When set, every storage call throws — what a browser with storage blocked (a private
    /// window on some browsers, or site data disabled) looks like from .NET.
    /// </summary>
    public bool ThrowOnAccess { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (ThrowOnAccess)
        {
            throw new JSException($"{storage} is not available.");
        }

        var key = args?[0] as string ?? string.Empty;

        if (identifier == $"{storage}.setItem")
        {
            Items[key] = (string)args![1]!;
        }
        else if (identifier == $"{storage}.removeItem")
        {
            Items.Remove(key);
        }
        else if (identifier == $"{storage}.getItem")
        {
            return ValueTask.FromResult((TValue)(object?)Items.GetValueOrDefault(key)!);
        }

        return default;
    }
}
