using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace RVS.API.Integrations.Availity;

/// <summary>
/// Minimal auth handler:
/// - If StaticBearerToken is set, applies it as Authorization: Bearer ...
/// - Otherwise no-op (assume infrastructure or future OAuth handler).
/// </summary>
public sealed class AvailityAuthHandler : DelegatingHandler
{
    private readonly AvailityOptions _options;

    public AvailityAuthHandler(IOptions<AvailityOptions> options)
    {
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.StaticBearerToken) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.StaticBearerToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
