using System.Security.Cryptography;
using System.Text;

namespace RVS.API.Integrations;

/// <summary>
/// Twilio's webhook signature (<c>X-Twilio-Signature</c>): HMAC-SHA1, keyed with the
/// subaccount's auth token, over the full URL Twilio called followed by every POST parameter's
/// name and value, sorted by name (ordinal), base64 encoded.
/// </summary>
public static class TwilioRequestSignature
{
    /// <summary>The header Twilio puts the signature in.</summary>
    public const string HeaderName = "X-Twilio-Signature";

    /// <summary>Computes the signature Twilio would send for this request.</summary>
    /// <param name="authToken">The subaccount's auth token.</param>
    /// <param name="url">The exact URL Twilio called, including any query string.</param>
    /// <param name="parameters">The form-encoded POST parameters.</param>
    public static string Compute(string authToken, string url, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(authToken);
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentNullException.ThrowIfNull(parameters);

        var data = new StringBuilder(url);
        foreach (var (name, value) in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            data.Append(name).Append(value);
        }

        var hash = HMACSHA1.HashData(Encoding.UTF8.GetBytes(authToken), Encoding.UTF8.GetBytes(data.ToString()));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Whether <paramref name="signature"/> is the one Twilio would send for this request,
    /// compared in fixed time so a forger cannot discover it a byte at a time.
    /// </summary>
    public static bool IsValid(
        string authToken, string url, IEnumerable<KeyValuePair<string, string>> parameters, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var expected = Compute(authToken, url, parameters);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }
}
