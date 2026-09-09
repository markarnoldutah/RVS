using System.Security.Cryptography;
using System.Text;

namespace RVS.Domain.Security;

/// <summary>
/// Shared helper for anonymous-access tokens (Spec X-5, decision issue #427).
///
/// One helper serves both token scopes:
/// <list type="bullet">
///   <item>the <b>per-customer status token</b> — <c>{emailPrefix}:{randomToken}</c>, where the
///     prefix is an email→partition hint for returning-customer prefill (Spec A-7);</item>
///   <item><b>C-7 per-request/per-action links</b> — a bare <see cref="GenerateRawToken"/> with no prefix.</item>
/// </list>
///
/// The raw token is handed to the customer once and is <b>never persisted</b>. Only
/// <see cref="ComputeHash"/> (SHA-256, base64url) is stored and indexed; validation hashes the
/// incoming token and looks up by hash.
/// </summary>
public static class AnonymousTokenHelper
{
    /// <summary>
    /// Random bytes in a raw token. 16 bytes = 128 bits, the Spec X-5 minimum.
    /// </summary>
    public const int TokenEntropyBytes = 16;

    private const int EmailPrefixBytes = 8;

    /// <summary>
    /// Generates a cryptographically random token with ≥128 bits of entropy, encoded base64url
    /// (URL-safe, unpadded). Used verbatim for C-7 action links and as the random suffix of the
    /// per-customer status token.
    /// </summary>
    public static string GenerateRawToken()
        => ToBase64Url(RandomNumberGenerator.GetBytes(TokenEntropyBytes));

    /// <summary>
    /// Derives the stable email→partition prefix: <c>base64url(SHA256(normalizedEmail)[..8])</c>.
    /// The email is normalized (trimmed, lower-cased) so the prefix is stable across intake sessions.
    /// </summary>
    public static string EmailPrefix(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalized = email.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return ToBase64Url(hash.AsSpan(0, EmailPrefixBytes));
    }

    /// <summary>
    /// Builds a per-customer status token: <c>{EmailPrefix(email)}:{GenerateRawToken()}</c>.
    /// Return value is the raw token to hand to the customer — hash it with
    /// <see cref="ComputeHash"/> before persisting.
    /// </summary>
    public static string GenerateStatusToken(string email)
        => $"{EmailPrefix(email)}:{GenerateRawToken()}";

    /// <summary>
    /// Computes <c>base64url(SHA256(utf8(rawToken)))</c> — the only representation of a token that
    /// is ever stored or indexed.
    /// </summary>
    public static string ComputeHash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return ToBase64Url(hash);
    }

    private static string ToBase64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
