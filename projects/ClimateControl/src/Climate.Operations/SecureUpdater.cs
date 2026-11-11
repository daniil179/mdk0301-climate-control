using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Semver;

namespace Climate.Operations;

public sealed record ReleaseManifest(string Version, string FileName, long Length, string Sha256, string Signature);

/// <summary>Stages a single data package and atomically switches its activation pointer.</summary>
public static class SecureUpdater
{
    public static byte[] Canonical(ReleaseManifest manifest) => Encoding.UTF8.GetBytes(
        string.Join("\n", "release-v1", manifest.Version, manifest.FileName,
            manifest.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), manifest.Sha256));

    public static ReleaseManifest CreateManifest(string version, byte[] package, RSA publisher)
    {
        var manifest = new ReleaseManifest(version, "ClimateControl-" + version + ".pkg", package.Length,
            Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(), "");
        return manifest with { Signature = Convert.ToBase64String(publisher.SignData(Canonical(manifest),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) };
    }

    public static string Install(ReleaseManifest manifest, byte[] package, string currentVersion,
        string installRoot, RSA trustedPublisher)
    {
        var next = SemVersion.Parse(manifest.Version, SemVersionStyles.Strict);
        var current = SemVersion.Parse(currentVersion, SemVersionStyles.Strict);
        if (next.IsPrerelease || next.ComparePrecedenceTo(current) <= 0)
            throw new InvalidOperationException("Release is not a newer stable version.");
        if (!Regex.IsMatch(manifest.FileName, @"\AClimateControl-[0-9A-Za-z.+-]+\.pkg\z"))
            throw new InvalidDataException("Invalid package file name.");
        if (manifest.Length != package.LongLength || package.Length > 10 * 1024 * 1024)
            throw new InvalidDataException("Invalid package length.");
        if (!trustedPublisher.VerifyData(Canonical(manifest), Convert.FromBase64String(manifest.Signature),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Publisher signature is invalid.");
        string digest = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        if (!string.Equals(digest, manifest.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException("Package hash mismatch.");
        string root = Path.GetFullPath(installRoot);
        string versions = Path.Combine(root, "versions");
        Directory.CreateDirectory(versions);
        string destination = Path.Combine(versions, manifest.Version);
        if (Directory.Exists(destination)) throw new IOException("Version already exists.");
        string staging = Path.Combine(root, ".stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            File.WriteAllBytes(Path.Combine(staging, manifest.FileName), package);
            File.WriteAllText(Path.Combine(staging, "manifest.json"), JsonSerializer.Serialize(manifest));
            Directory.Move(staging, destination);
            string temporaryPointer = Path.Combine(root, "active-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temporaryPointer, JsonSerializer.Serialize(new { version = manifest.Version,
                package = "versions/" + manifest.Version + "/" + manifest.FileName }));
            File.Move(temporaryPointer, Path.Combine(root, "active.json"), true);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
        return destination;
    }
}
