using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace RVS.Domain.Security;

/// <summary>
/// The single-use token carried by an A-14 advisor intake invite
/// (<c>Spec A-14</c>, <c>Spec X-5</c>, issue #663).
///
/// The raw token exists in exactly two places: the texted link and the customer's browser. RVS
/// stores only <see cref="Hash"/>, which is also the invite's Cosmos document id, so a lookup is
/// a point read and a leaked database holds nothing that opens an invite.
///
/// This is new code on purpose. <c>GlobalCustomerAcct.MagicLinkToken</c> is stored in plaintext
/// and is not a model to copy.
/// </summary>
public static class InviteToken
{
    /// <summary>Random bytes per token: 256 bits, twice X-5's 128-bit floor.</summary>
    public const int ByteLength = 32;

    /// <summary>Length of the unpadded base64url encoding of <see cref="ByteLength"/> bytes.</summary>
    public const int Length = 43;

    /// <summary>
    /// Mints a new token: <see cref="ByteLength"/> bytes from a CSPRNG, unpadded base64url.
    /// </summary>
    public static string Generate() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));

    /// <summary>
    /// The value RVS stores in place of the token: lowercase hex SHA-256, 64 characters.
    /// </summary>
    /// <param name="token">A token produced by <see cref="Generate"/>.</param>
    /// <exception cref="ArgumentException">The token is blank or not token-shaped.</exception>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (!IsWellFormed(token))
        {
            throw new ArgumentException("Not a well-formed invite token.", nameof(token));
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> has the shape of a token: exactly <see cref="Length"/>
    /// base64url characters. A shape check, not a lookup — it says nothing about whether the
    /// invite exists. Callers handling a token from a URL use it to drop junk before it reaches
    /// storage.
    /// </summary>
    public static bool IsWellFormed(string? candidate)
    {
        if (candidate is null || candidate.Length != Length)
        {
            return false;
        }

        foreach (var c in candidate)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
