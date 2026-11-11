using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Climate.Core;
using Climate.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

if (args.Length == 0) throw new ArgumentException("Commands: demo <new-folder>, backup <database> <backup>, sat <checks.json> <SAT.md>, metrics");
if (args[0] == "backup" && args.Length == 3)
{
    Console.WriteLine("Backup SHA-256: " + BackupService.Create(args[1], args[2])); return;
}
if (args[0] == "sat" && args.Length == 3) { SatProtocol.ConvertJson(args[1], args[2]); Console.WriteLine("SAT Markdown generated."); return; }
if (args[0] == "metrics")
{
    Console.WriteLine(JsonSerializer.Serialize(ReliabilityMetrics.Calculate(720, 20, 35, 45), new JsonSerializerOptions { WriteIndented = true })); return;
}
if (args[0] != "demo" || args.Length != 2) throw new ArgumentException("Invalid command arguments.");
string output = Path.GetFullPath(args[1]);
if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use a new empty demo folder.");
Directory.CreateDirectory(output);
var checks = new List<SatCheck>();
void Check(string id, string description, bool passed, string evidence)
{
    checks.Add(new(id, description, passed ? "PASS" : "FAIL", evidence));
    Console.WriteLine($"{id}: {(passed ? "PASS" : "FAIL")} — {description}");
}
using (var certs = new DemoCertificates())
{
    bool trusted = CertificateValidation.Validate(certs.Publisher, certs.Root, "publisher.campus.test", offlineTestOnly: true);
    Check("CERT", "X.509 chain, validity, DNS and Code Signing EKU", trusted, "Offline test CA; revocation is not tested.");
    File.WriteAllBytes(Path.Combine(output, "publisher-public.cer"), certs.Publisher.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
    using var publisherKey = certs.Publisher.GetRSAPrivateKey()!;
    using var publicKey = certs.Publisher.GetRSAPublicKey()!;
    byte[] payload = Encoding.UTF8.GetBytes("Climate Control educational package 1.1.0");
    var manifest = SecureUpdater.CreateManifest("1.1.0", payload, publisherKey);
    string installRoot = Path.Combine(output, "installation");
    SecureUpdater.Install(manifest, payload, "1.0.0", installRoot, publicKey);
    string pointer = File.ReadAllText(Path.Combine(installRoot, "active.json"));
    Check("UPDATE", "Signed SemVer 1.1.0 package activates", pointer.Contains("1.1.0"), "installation/active.json");
    var next = SecureUpdater.CreateManifest("1.2.0", payload, publisherKey);byte[] broken = payload.ToArray();broken[0] ^= 1;
    bool rejected = false;
    try { SecureUpdater.Install(next, broken, "1.1.0", installRoot, publicKey); }
    catch (InvalidDataException) { rejected = true; }
    Check("SHA256", "Corrupt package is rejected; active pointer stays unchanged", rejected && File.ReadAllText(Path.Combine(installRoot, "active.json")) == pointer, "No installation of 1.2.0");
}
string path = Path.Combine(output, "outbox.db");
await using (var db = OutboxContext.Open(path))
{
    var migrator = db.GetService<IMigrator>();await migrator.MigrateAsync("20261104000100_Initial");
    Guid oldId = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Outbox (Id, SensorId, Sequence, Payload, CreatedAtUtc, Attempts, State) VALUES ({oldId}, {"DVC-001"}, {100}, {"legacy demonstration row"}, {DateTime.UtcNow}, {0}, {0})");
    await migrator.MigrateAsync();
    var old = await db.Messages.SingleAsync();
    Check("MIGRATION", "Expand migration preserves legacy row and assigns Priority=0", old.Priority == 0 && old.Payload == "legacy demonstration row", "outbox.db / __EFMigrationsHistory");
    var transport = new DemoTransport();var buffer = new OfflineBuffer(db, transport, TimeSpan.FromMilliseconds(10));
    var request = new TelemetryRequest("DVC-001", 101, 23.5m, 45m, DateTimeOffset.UtcNow.ToString("O"));
    Guid id = await buffer.EnqueueAsync(request);
    var state = await buffer.DeliverAsync(id);
    Check("OFFLINE", "SQLite + Polly: two transient failures, third attempt accepted", state == DeliveryState.Delivered && transport.Calls == 3, "Attempts=3; persisted Delivered state");
}
string backup = Path.Combine(output, "backup.db");string digest = BackupService.Create(path, backup);
string restoredPath = Path.Combine(output, "restored.db");BackupService.Restore(backup, restoredPath, digest);
await using (var restored = OutboxContext.Open(restoredPath))
    Check("BACKUP", "Online backup integrity and restore preserve two rows", await restored.Messages.CountAsync() == 2, "backup.db.sha256 / restored.db");
string hash = PasswordService.Hash("Synthetic-demo-passphrase!2026");
Check("BCRYPT", "BCrypt accepts correct and rejects wrong demo password", PasswordService.Verify("Synthetic-demo-passphrase!2026", hash) && !PasswordService.Verify("wrong-synthetic-passphrase", hash), "Only verification booleans are logged.");
Check("RBAC", "Operator cannot change configuration; engineer can", !Rbac.IsAllowed(UserRole.Operator, Operation.ChangeConfiguration) && Rbac.IsAllowed(UserRole.Engineer, Operation.ChangeConfiguration), "Closed role/permission matrix.");
checks.Add(new("DPAPI", "Windows DPAPI and user-computer checks", "DEFERRED", "Excluded from this stage; no DPAPI implementation executed."));
File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(output, "SAT.md"), SatProtocol.ToMarkdown(checks));
File.WriteAllText(Path.Combine(output, "reliability.json"), JsonSerializer.Serialize(ReliabilityMetrics.Calculate(720, 20, 35, 45), new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("SAT.md saved. DPAPI: DEFERRED.");
Environment.ExitCode = checks.Any(x => x.Status == "FAIL") ? 1 : 0;

public sealed class DemoTransport : IOutboxTransport
{
    public int Calls { get; private set; }
    public Task<DeliveryOutcome> SendAsync(string payload, CancellationToken ct)
    {
        Calls++;
        if (Calls <= 2) throw new HttpRequestException("Simulated offline interval.");
        return Task.FromResult(DeliveryOutcome.Accepted);
    }
}
