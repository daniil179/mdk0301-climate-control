using System.Security.Cryptography;
using Climate.Operations;
using Xunit;

namespace Climate.UnitTests;

public sealed class WindowsProtectionTests
{
    [Fact]
    public void WindowsSecretRoundTripDoesNotExposePlaintext()
    {
        if (!OperatingSystem.IsWindows()) return;
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] blob = WindowsSecretProtection.Protect(key);
        Assert.NotEqual(key, blob);
        Assert.Equal(key, WindowsSecretProtection.Unprotect(blob));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void TamperingWithDpapiBlobIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        byte[] blob = WindowsSecretProtection.Protect(RandomNumberGenerator.GetBytes(32));
        blob[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => WindowsSecretProtection.Unprotect(blob));
    }

    [Fact]
    public void DifferentSecretPurposeIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        byte[] blob = WindowsSecretProtection.Protect(RandomNumberGenerator.GetBytes(32), "DeviceKey");
        Assert.Throws<CryptographicException>(() => WindowsSecretProtection.Unprotect(blob, "UpdaterKey"));
    }
}
