using System.Net;

namespace RVS.API.RateLimiting;

/// <summary>
/// Resolves the caller's client IP for per-IP rate-limit partitioning (<c>Spec X-5</c>:
/// anonymous endpoints are "rate-limited per IP"). The API runs behind Azure
/// infrastructure that terminates the TCP connection, so the originating client address
/// arrives in the <c>X-Forwarded-For</c> header; the socket remote address is only a
/// fallback for direct calls (e.g. local development).
/// </summary>
public static class ClientIpResolver
{
    /// <summary>Header Azure Front Door / App Service uses to carry the originating client IP.</summary>
    public const string ForwardedForHeader = "X-Forwarded-For";

    /// <summary>Partition key used when no caller address can be determined.</summary>
    public const string UnknownKey = "unknown";

    /// <summary>
    /// Returns a stable partition key identifying the caller: the first address in
    /// <c>X-Forwarded-For</c> when present, otherwise the socket remote IP, otherwise
    /// <see cref="UnknownKey"/>. A <c>:port</c> suffix on an IPv4 entry is stripped so
    /// the same client maps to one partition regardless of ephemeral source port.
    /// </summary>
    public static string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var forwardedFor = context.Request.Headers[ForwardedForHeader].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // "X-Forwarded-For: client, proxy1, proxy2" — the originating client is first.
            var first = forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(first))
            {
                return Normalize(first);
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? UnknownKey;
    }

    private static string Normalize(string address)
    {
        if (IPAddress.TryParse(address, out var parsed))
        {
            return parsed.ToString();
        }

        // "1.2.3.4:56789" — an IPv4 entry carrying a source port. A single colon
        // distinguishes it from IPv6, which always has several.
        var colon = address.IndexOf(':');
        if (colon > 0 && address.IndexOf(':', colon + 1) < 0)
        {
            var host = address[..colon];
            if (IPAddress.TryParse(host, out var hostOnly))
            {
                return hostOnly.ToString();
            }
        }

        return address;
    }
}
