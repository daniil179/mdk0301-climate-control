using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Climate.Operations;

public static class PasswordService
{
    public static string Hash(string password)
    {
        if (password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new ArgumentException("Password must be at least 12 characters and at most 72 UTF-8 bytes.");
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }
    public static bool Verify(string password, string hash) =>
        Encoding.UTF8.GetByteCount(password) <= 72 && BCrypt.Net.BCrypt.Verify(password, hash);
}

public enum UserRole { Operator, Engineer, Administrator }
public enum Operation { ReadTelemetry, ChangeConfiguration, EnrollDevice }
public static class Rbac
{
    public static bool IsAllowed(UserRole role, Operation action) => (role, action) switch
    {
        (UserRole.Operator or UserRole.Engineer or UserRole.Administrator, Operation.ReadTelemetry) => true,
        (UserRole.Engineer or UserRole.Administrator, Operation.ChangeConfiguration) => true,
        (UserRole.Administrator, Operation.EnrollDevice) => true,
        _ => false
    };
}

public static class CertificateValidation
{
    public const string CodeSigning = "1.3.6.1.5.5.7.3.3";
    public static bool Validate(X509Certificate2 certificate, X509Certificate2 trustedRoot,
        string expectedDnsName, bool offlineTestOnly = false)
    {
        if (certificate.GetNameInfo(X509NameType.DnsName, false) != expectedDnsName) return false;
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(CodeSigning));
        chain.ChainPolicy.RevocationMode = offlineTestOnly ? X509RevocationMode.NoCheck : X509RevocationMode.Online;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        return chain.Build(certificate);
    }
}

/// <summary>Ephemeral test certificates. Only public certificates may be exported by the demo.</summary>
public sealed class DemoCertificates : IDisposable
{
    public X509Certificate2 Root { get; }
    public X509Certificate2 Publisher { get; }
    public DemoCertificates(bool expired = false)
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Climate Demo Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var issuer = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddYears(2));
        Root = new X509Certificate2(issuer.Export(X509ContentType.Cert));
        using var leafKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=publisher.campus.test", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(CertificateValidation.CodeSigning) }, true));
        var san = new SubjectAlternativeNameBuilder();san.AddDnsName("publisher.campus.test");request.CertificateExtensions.Add(san.Build());
        using var publicLeaf = request.Create(issuer, DateTimeOffset.UtcNow.AddDays(expired ? -10 : -1),
            DateTimeOffset.UtcNow.AddDays(expired ? -1 : 30), RandomNumberGenerator.GetBytes(16));
        Publisher = publicLeaf.CopyWithPrivateKey(leafKey);
    }
    public void Dispose() { Root.Dispose(); Publisher.Dispose(); }
}
