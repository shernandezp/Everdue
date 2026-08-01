using System.Security.Cryptography;
using System.Text;

namespace Everdue.Server.Domain;

/// <summary>A freshly minted key: the token to show the creator once, and the two columns to store.</summary>
public sealed record NewApiKey(string Token, string Prefix, string Hash);

/// <summary>
/// The token format and its hashing, in one place.
///
/// <para><c>evd_{prefix}_{secret}</c> — a recognisable scheme prefix (so a leaked string is
/// identifiable in a log or by a secret scanner), a lookup prefix, and 32 random bytes. Both parts are
/// lowercase hex: the separator is <c>_</c>, so an alphabet that can itself contain <c>_</c> or <c>-</c>
/// would make the token ambiguous to split.</para>
///
/// <para><strong>SHA-256, not PBKDF2 or Argon2, and that is deliberate.</strong> A slow hash exists to
/// make guessing a low-entropy human password expensive. This secret is 256 bits of cryptographic
/// randomness: there is nothing to guess, and a slow hash would only add latency to every
/// authenticated request.</para>
/// </summary>
public static class ApiKeyToken
{
    public const string Scheme = "evd";

    /// <summary>Hex characters in the lookup prefix. Long enough that a prefix match is normally unique.</summary>
    public const int PrefixLength = 12;

    private const int SecretBytes = 32;

    public static NewApiKey Create()
    {
        var prefix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(PrefixLength / 2));
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SecretBytes));

        return new NewApiKey($"{Scheme}_{prefix}_{secret}", prefix, Hash(secret));
    }

    /// <summary>
    /// Splits a presented token. Returns false for anything that is not shaped like one of ours, so a
    /// malformed header never reaches the database.
    /// </summary>
    public static bool TryParse(string? token, out string prefix, out string secret)
    {
        prefix = string.Empty;
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Trim().Split('_');

        if (parts.Length != 3
            || parts[0] != Scheme
            || parts[1].Length != PrefixLength
            || parts[2].Length != SecretBytes * 2)
        {
            return false;
        }

        prefix = parts[1];
        secret = parts[2];
        return true;
    }

    public static string Hash(string secret)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>
    /// Compared in fixed time. The stored hash is not itself a secret, but comparing hashes with
    /// <c>==</c> is the habit that eventually gets applied to something that is.
    /// </summary>
    public static bool Matches(string storedHash, string presentedSecret)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(Hash(presentedSecret)));
}
