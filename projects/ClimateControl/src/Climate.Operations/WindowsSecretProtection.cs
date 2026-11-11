using System.Security.Cryptography;
using System.Text;

namespace Climate.Operations;

/// <summary>Windows DPAPI protection for secrets owned by the current Windows user.</summary>
public static class WindowsSecretProtection
{
    public static byte[] Protect(byte[] plaintext, string purpose = "DeviceKey")
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI is required.");
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0) throw new ArgumentException("Secret must not be empty.", nameof(plaintext));
        return ProtectedData.Protect(plaintext, Entropy(purpose), DataProtectionScope.CurrentUser);
    }

    public static byte[] Unprotect(byte[] protectedData, string purpose = "DeviceKey")
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI is required.");
        ArgumentNullException.ThrowIfNull(protectedData);
        return ProtectedData.Unprotect(protectedData, Entropy(purpose), DataProtectionScope.CurrentUser);
    }

    private static byte[] Entropy(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return SHA256.HashData(Encoding.UTF8.GetBytes("ClimateControl/v1/" + purpose));
    }
}
