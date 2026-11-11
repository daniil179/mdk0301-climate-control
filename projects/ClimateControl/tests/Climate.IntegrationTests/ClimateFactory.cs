using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Climate.Core;
using Climate.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Climate.IntegrationTests;

public sealed class TestRegistry : IDeviceRegistry
{
    // Synthetic test keys, never production enrollment secrets.
    public static readonly byte[] TestKey = SHA256.HashData(Encoding.UTF8.GetBytes("SYNTHETIC-TEST-KEY-ONLY"));
    public DeviceIdentity? Find(string id, string key) => key != "key-1" ? null : id switch
    {
        "DVC-001" => new(id, "A101", TestKey, true),
        "DVC-002" => new(id, "B202", SHA256.HashData(TestKey), true),
        "DVC-003" => new(id, "A101", TestKey, false),
        _ => null
    };
}

public sealed class ClimateFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ClimateControl.sln")))
            root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("ClimateControl.sln was not found.");
        builder.UseContentRoot(Path.Combine(root.FullName, "src", "Climate.Api"));
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDeviceRegistry>();
            services.AddSingleton<IDeviceRegistry, TestRegistry>();
            services.RemoveAll<DbContextOptions<ClimateDbContext>>();
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            services.AddSingleton(connection);
            services.AddDbContext<ClimateDbContext>(o => o.UseSqlite(connection));
        });
    }

    public static TelemetryRequest Payload(long sequence = 1) =>
        new("DVC-001", sequence, 23.5m, 45m, DateTimeOffset.UtcNow.ToString("O"));

    public static HttpRequestMessage Signed(TelemetryRequest payload,
        string device = "DVC-001", string? nonce = null, long? timestamp = null,
        string? signedBodyOverride = null, byte[]? key = null)
    {
        string body = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return SignedRaw(body, device, nonce, timestamp, signedBodyOverride, key);
    }

    public static HttpRequestMessage SignedRaw(string body, string device = "DVC-001",
        string? nonce = null, long? timestamp = null, string? signedBodyOverride = null, byte[]? key = null)
    {
        string stamp = (timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        string n = nonce ?? Guid.NewGuid().ToString("N");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telemetry");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        byte[] canonical = HmacProtocol.Canonical("POST", "/api/v1/telemetry", device,
            "key-1", stamp, n, Encoding.UTF8.GetBytes(signedBodyOverride ?? body));
        request.Headers.Add("X-Device-Id", device);
        request.Headers.Add("X-Key-Id", "key-1");
        request.Headers.Add("X-Timestamp", stamp);
        request.Headers.Add("X-Nonce", n);
        request.Headers.Add("X-Signature", HmacProtocol.Sign(key ?? TestRegistry.TestKey, canonical));
        return request;
    }
}
