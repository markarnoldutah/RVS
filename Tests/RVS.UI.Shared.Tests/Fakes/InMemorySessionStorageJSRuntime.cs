using Microsoft.JSInterop;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// <see cref="IJSRuntime"/> that backs <c>sessionStorage.getItem</c>, <c>setItem</c> and
/// <c>removeItem</c> with a dictionary, so persistence can be round-tripped in a unit test.
/// Every other call returns the default value.
/// </summary>
internal sealed class InMemorySessionStorageJSRuntime : IJSRuntime
{
    private readonly Dictionary<string, string> _items = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = args?[0] as string ?? string.Empty;

        switch (identifier)
        {
            case "sessionStorage.setItem":
                _items[key] = (string)args![1]!;
                break;
            case "sessionStorage.removeItem":
                _items.Remove(key);
                break;
            case "sessionStorage.getItem":
                return ValueTask.FromResult((TValue)(object?)_items.GetValueOrDefault(key)!);
        }

        return default;
    }
}
