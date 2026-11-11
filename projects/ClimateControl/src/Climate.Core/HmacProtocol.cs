using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Climate.Core;

/// <summary>Versioned HMAC-SHA256 protocol over method, path, identity and raw body hash.</summary>
public static class HmacProtocol
{
    /// <summary>Builds the exact UTF-8 canonical message; newline is always LF.</summary>
    public static byte[] Canonical(string method, string path, string device,
        string keyId, string timestamp, string nonce, byte[] body)
    {
        string hash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        return Encoding.UTF8.GetBytes(string.Join("\n", "v1", method.ToUpperInvariant(),
            path, device, keyId, timestamp, nonce, hash));
    }

    /// <summary>Produces a lower-case hex signature for the canonical request.</summary>
    public static string Sign(byte[] key, byte[] canonical) =>
        Convert.ToHexString(HMACSHA256.HashData(key, canonical)).ToLowerInvariant();

    /// <summary>Compares decoded signatures in constant time; malformed input is rejected.</summary>
    public static bool Verify(byte[] key, byte[] canonical, string supplied)
    {
        if (supplied.Length != 64)
            return false;
        try
        {
            byte[] actual = Convert.FromHexString(supplied);
            return CryptographicOperations.FixedTimeEquals(
                HMACSHA256.HashData(key, canonical), actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Accepts an integral Unix timestamp within 120 seconds, inclusively.</summary>
    public static bool IsFresh(string text, DateTimeOffset now)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
            out long seconds))
            return false;
        long current = now.ToUnixTimeSeconds();
        return seconds >= current - 120 && seconds <= current + 120;
    }
}
