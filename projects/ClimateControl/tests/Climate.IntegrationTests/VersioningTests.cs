using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Climate.Core;
using Climate.Operations;
using Climate.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Climate.IntegrationTests;

public sealed record OldClientReading(string SensorId, decimal TemperatureC, decimal HumidityPercent);
public sealed class VersioningTests
{
    private static HttpRequestMessage Get(string path)
    {
        string stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();string nonce = Guid.NewGuid().ToString("N");
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Device-Id", "DVC-001");request.Headers.Add("X-Key-Id", "key-1");
        request.Headers.Add("X-Timestamp", stamp);request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Signature", HmacProtocol.Sign(TestRegistry.TestKey,
            HmacProtocol.Canonical("GET", path, "DVC-001", "key-1", stamp, nonce, [])));
        return request;
    }
    private static async Task Seed(HttpClient client) =>
        (await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload()))).StatusCode.Should().Be(HttpStatusCode.Created);

    [Fact]
    public async Task V01_StrictOldClientStillReadsV1()
    {
        using var factory = new ClimateFactory();using var client = factory.CreateClient();await Seed(client);
        var response = await client.SendAsync(Get("/api/v1/climate/readings/latest/DVC-001"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<OldClientReading>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
        dto!.TemperatureC.Should().Be(23.5m);dto.HumidityPercent.Should().Be(45m);
        Save("v1-response.json", json);
    }
    [Fact]
    public async Task V02_V2UsesExplicitUnitWithoutChangingV1()
    {
        using var factory = new ClimateFactory();using var client = factory.CreateClient();await Seed(client);
        var response = await client.SendAsync(Get("/api/v2/climate/readings/latest/DVC-001"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("temperature").GetProperty("unit").GetString().Should().Be("Cel");
        Save("v2-response.json", json);
    }
    [Fact]
    public async Task V03_ReadWithoutHmacIsUnauthorized()
    {
        using var factory = new ClimateFactory();using var client = factory.CreateClient();
        (await client.GetAsync("/api/v1/climate/readings/latest/DVC-001")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task V04_ReadingAnotherSensorIsForbidden()
    {
        using var factory = new ClimateFactory();using var client = factory.CreateClient();
        (await client.SendAsync(Get("/api/v1/climate/readings/latest/DVC-002"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task V05_UnknownVersionIsNotRouted()
    {
        using var factory = new ClimateFactory();using var client = factory.CreateClient();
        (await client.SendAsync(Get("/api/v9/climate/readings/latest/DVC-001"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task V06_RealOutboxDeliversThroughHmacApi()
    {
        using var factory = new ClimateFactory();using var client = factory.CreateClient();
        string path = Path.Combine(Path.GetTempPath(), "climate-outbox-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using var db = OutboxContext.Open(path);await db.Database.MigrateAsync();
            var buffer = new OfflineBuffer(db, new HmacOutboxTransport(client, "DVC-001", TestRegistry.TestKey), TimeSpan.Zero);
            var id = await buffer.EnqueueAsync(ClimateFactory.Payload());
            (await buffer.DeliverAsync(id)).Should().Be(DeliveryState.Delivered);
            using var scope = factory.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<ClimateDbContext>().Telemetry.CountAsync()).Should().Be(1);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);File.Delete(path+"-wal");File.Delete(path+"-shm"); }
    }
    private static void Save(string name, string text)
    {
        string? folder = Environment.GetEnvironmentVariable("CLIMATE_EVIDENCE");
        if (folder is not null) File.WriteAllText(Path.Combine(folder, name), text);
    }
}
