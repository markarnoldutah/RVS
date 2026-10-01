using Microsoft.Extensions.Options;
using RVS.API.Options;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Resolves every location to the one shared number in <see cref="SmsOptions.FromPhoneNumber"/>
/// (issue #661). Replace this, not its callers, when locations get their own numbers.
/// </summary>
public sealed class ConfiguredSmsSenderNumberResolver : ISmsSenderNumberResolver
{
    private readonly SmsOptions _options;

    public ConfiguredSmsSenderNumberResolver(IOptions<SmsOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<string?> ResolveAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        return Task.FromResult(
            string.IsNullOrWhiteSpace(_options.FromPhoneNumber) ? null : _options.FromPhoneNumber);
    }

    /// <inheritdoc />
    public Task<string?> ResolveDefaultAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(
            string.IsNullOrWhiteSpace(_options.FromPhoneNumber) ? null : _options.FromPhoneNumber);
}
