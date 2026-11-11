using System.Security.Cryptography;
using System.Text;
using Climate.Core;
using Climate.Operations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Semver;
using Xunit;

namespace Climate.UnitTests;

public sealed class TestDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "climate-ops-" + Guid.NewGuid().ToString("N"));
    public TestDirectory() => Directory.CreateDirectory(Root);
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(Root, true); }
}

public sealed class ControlledTransport(int failures, DeliveryOutcome outcome = DeliveryOutcome.Accepted) : IOutboxTransport
{
    public int Calls { get; private set; }
    public Task<DeliveryOutcome> SendAsync(string payload, CancellationToken ct)
    {
        Calls++;
        if (Calls <= failures) throw new HttpRequestException("Synthetic temporary outage.");
        return Task.FromResult(outcome);
    }
}

public sealed class OperationsTests
{
    private static TelemetryRequest Payload(long sequence = 1) => new("DVC-001", sequence, 23.5m, 45m, "2026-11-04T09:00:00Z");

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [InlineData("1.0.0-rc.1", "1.0.0", -1)]
    [InlineData("1.0.0+build.1", "1.0.0+build.2", 0)]
    [InlineData("1.10.0", "1.2.0", 1)]
    public void O01_SemverPrecedence(string a, string b, int expected) =>
        Math.Sign(SemVersion.Parse(a, SemVersionStyles.Strict).ComparePrecedenceTo(SemVersion.Parse(b, SemVersionStyles.Strict))).Should().Be(expected);

    [Theory]
    [InlineData("1.2")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-01")]
    public void O02_InvalidSemverIsRejected(string value)
    {
        Action parse = () => SemVersion.Parse(value, SemVersionStyles.Strict);
        parse.Should().Throw<FormatException>();
    }

    [Fact]
    public void O03_ValidSignedPackageActivates()
    {
        using var temp = new TestDirectory();using var key = RSA.Create(2048);
        byte[] payload = Encoding.UTF8.GetBytes("synthetic package v1.1.0");
        var manifest = SecureUpdater.CreateManifest("1.1.0", payload, key);
        var folder = SecureUpdater.Install(manifest, payload, "1.0.0", temp.Root, key);
        File.ReadAllBytes(Path.Combine(folder, manifest.FileName)).Should().Equal(payload);
        File.ReadAllText(Path.Combine(temp.Root, "active.json")).Should().Contain("1.1.0");
    }

    [Fact]
    public void O04_CorruptedPackageDoesNotChangeActivePointer()
    {
        using var temp = new TestDirectory();using var key = RSA.Create(2048);
        File.WriteAllText(Path.Combine(temp.Root, "active.json"), "previous pointer");
        byte[] bytes = [1, 2, 3];var manifest = SecureUpdater.CreateManifest("1.1.0", bytes, key);
        Action install = () => SecureUpdater.Install(manifest, [1, 2, 9], "1.0.0", temp.Root, key);
        install.Should().Throw<InvalidDataException>();
        File.ReadAllText(Path.Combine(temp.Root, "active.json")).Should().Be("previous pointer");
    }

    [Fact]
    public void O05_WrongPublisherIsRejected()
    {
        using var temp = new TestDirectory();using var signer = RSA.Create(2048);using var wrong = RSA.Create(2048);
        var manifest = SecureUpdater.CreateManifest("1.1.0", [1], signer);
        Action install = () => SecureUpdater.Install(manifest, [1], "1.0.0", temp.Root, wrong);
        install.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("0.9.0")]
    [InlineData("1.0.0")]
    [InlineData("1.1.0-rc.1")]
    public void O06_DowngradeAndPrereleaseAreRejected(string version)
    {
        using var temp = new TestDirectory();using var key = RSA.Create(2048);
        var manifest = SecureUpdater.CreateManifest(version, [1], key);
        Action install = () => SecureUpdater.Install(manifest, [1], "1.0.0", temp.Root, key);
        install.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task O07_QueueSurvivesContextRestart()
    {
        using var temp = new TestDirectory();string path = Path.Combine(temp.Root, "outbox.db");Guid id;
        await using (var db = OutboxContext.Open(path))
        {
            await db.Database.MigrateAsync();
            id = await new OfflineBuffer(db, new ControlledTransport(0), TimeSpan.Zero).EnqueueAsync(Payload());
        }
        await using var reopened = OutboxContext.Open(path);
        (await reopened.Messages.SingleAsync()).Id.Should().Be(id);
    }

    [Fact]
    public async Task O08_TransientOutageRetriesTwiceThenDelivers()
    {
        using var temp = new TestDirectory();await using var db = OutboxContext.Open(Path.Combine(temp.Root, "outbox.db"));
        await db.Database.MigrateAsync();var transport = new ControlledTransport(2);
        var buffer = new OfflineBuffer(db, transport, TimeSpan.Zero);Guid id = await buffer.EnqueueAsync(Payload());
        (await buffer.DeliverAsync(id)).Should().Be(DeliveryState.Delivered);
        transport.Calls.Should().Be(3);(await db.Messages.SingleAsync()).Attempts.Should().Be(3);
    }

    [Fact]
    public async Task O09_ExhaustedRetriesKeepPendingMessage()
    {
        using var temp = new TestDirectory();await using var db = OutboxContext.Open(Path.Combine(temp.Root, "outbox.db"));
        await db.Database.MigrateAsync();var buffer = new OfflineBuffer(db, new ControlledTransport(100), TimeSpan.Zero);
        Guid id = await buffer.EnqueueAsync(Payload());
        (await buffer.DeliverAsync(id)).Should().Be(DeliveryState.Pending);
        (await db.Messages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task O10_ConflictIsNotSilentlyAcknowledged()
    {
        using var temp = new TestDirectory();await using var db = OutboxContext.Open(Path.Combine(temp.Root, "outbox.db"));
        await db.Database.MigrateAsync();var buffer = new OfflineBuffer(db, new ControlledTransport(0, DeliveryOutcome.Conflict), TimeSpan.Zero);
        var id = await buffer.EnqueueAsync(Payload());
        (await buffer.DeliverAsync(id)).Should().Be(DeliveryState.NeedsReview);
        (await db.Messages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task O11_MigrationPreservesOldRowAndOldWriter()
    {
        using var temp = new TestDirectory();await using var db = OutboxContext.Open(Path.Combine(temp.Root, "outbox.db"));
        var migrator = db.GetService<IMigrator>();await migrator.MigrateAsync("20261104000100_Initial");
        Guid a = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Outbox (Id, SensorId, Sequence, Payload, CreatedAtUtc, Attempts, State) VALUES ({a}, {"DVC-001"}, {1}, {"old payload"}, {DateTime.UtcNow}, {0}, {0})");
        await migrator.MigrateAsync();
        (await db.Messages.SingleAsync()).Priority.Should().Be(0);
        Guid b = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Outbox (Id, SensorId, Sequence, Payload, CreatedAtUtc, Attempts, State) VALUES ({b}, {"DVC-001"}, {2}, {"old writer after migration"}, {DateTime.UtcNow}, {0}, {0})");
        (await db.Messages.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task O12_BackupRestoresCommittedData()
    {
        using var temp = new TestDirectory();string dbPath = Path.Combine(temp.Root, "outbox.db");
        await using (var db = OutboxContext.Open(dbPath))
        {
            await db.Database.MigrateAsync();await new OfflineBuffer(db, new ControlledTransport(0), TimeSpan.Zero).EnqueueAsync(Payload());
        }
        string backup = Path.Combine(temp.Root, "backup.db");string hash = BackupService.Create(dbPath, backup);
        string target = Path.Combine(temp.Root, "restored.db");BackupService.Restore(backup, target, hash);
        await using var restored = OutboxContext.Open(target);(await restored.Messages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void O13_BcryptChecksCorrectAndWrongPassword()
    {
        const string password = "Demo-only-passphrase!2026";
        string hash = PasswordService.Hash(password);
        PasswordService.Verify(password, hash).Should().BeTrue();
        PasswordService.Verify("wrong-demo-password", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Operator, Operation.ReadTelemetry, true)]
    [InlineData(UserRole.Operator, Operation.ChangeConfiguration, false)]
    [InlineData(UserRole.Engineer, Operation.ChangeConfiguration, true)]
    [InlineData(UserRole.Engineer, Operation.EnrollDevice, false)]
    [InlineData(UserRole.Administrator, Operation.EnrollDevice, true)]
    public void O14_Rbac(UserRole role, Operation operation, bool expected) => Rbac.IsAllowed(role, operation).Should().Be(expected);

    [Fact]
    public void O15_TrustedCertificateIsAcceptedOffline()
    {
        using var certs = new DemoCertificates();
        CertificateValidation.Validate(certs.Publisher, certs.Root, "publisher.campus.test", true).Should().BeTrue();
    }
    [Fact]
    public void O16_WrongRootAndNameAreRejected()
    {
        using var certs = new DemoCertificates();using var wrong = new DemoCertificates();
        CertificateValidation.Validate(certs.Publisher, wrong.Root, "publisher.campus.test", true).Should().BeFalse();
        CertificateValidation.Validate(certs.Publisher, certs.Root, "wrong.campus.test", true).Should().BeFalse();
    }
    [Fact]
    public void O17_ExpiredCertificateIsRejected()
    {
        using var certs = new DemoCertificates(true);
        CertificateValidation.Validate(certs.Publisher, certs.Root, "publisher.campus.test", true).Should().BeFalse();
    }
    [Fact]
    public void O18_ReliabilityUsesUptimeNotObservationPeriod()
    {
        var result = ReliabilityMetrics.Calculate(720, 20, 35, 45);
        result.MtbfHours.Should().BeApproximately(239.444444444, 0.000001);
        result.MttrMinutes.Should().BeApproximately(33.333333333, 0.000001);
        result.AvailabilityPercent.Should().BeApproximately(99.7685185185, 0.000001);
    }
    [Fact]
    public void O19_NoFailuresHasNoMeasuredMtbf() => ReliabilityMetrics.Calculate(24).MtbfHours.Should().BeNull();
    [Fact]
    public void O20_SatPreservesDeferredStatus() => SatProtocol.ToMarkdown([new("WIN", "Windows DPAPI", "DEFERRED", "Excluded")]).Should().Contain("DEFERRED");
}
