using System.Net;
using System.Text.Json;
using Climate.Api.Security;
using Climate.Core;
using Climate.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Climate.IntegrationTests;

public sealed class ApiTests
{
    [Fact]
    public async Task I01_ValidSignedMeasurementIsPersisted()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var payload = ClimateFactory.Payload();
        var response = await client.SendAsync(ClimateFactory.Signed(payload));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ClimateDbContext>().Telemetry.SingleAsync();
        row.SensorId.Should().Be("DVC-001"); row.RoomId.Should().Be("A101"); row.TemperatureC.Should().Be(23.5m);
        Evidence("accepted-response.json", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I02_MissingSignatureIsUnauthorized()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/v1/telemetry", new StringContent("{}"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task S01_TamperedTemperatureIsRejectedBeforeWrite()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var payload = ClimateFactory.Payload();
        string original = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var response = await client.SendAsync(ClimateFactory.Signed(payload with { TemperatureC = 83.5m }, signedBodyOverride: original));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ClimateDbContext>().Telemetry.CountAsync()).Should().Be(0);
        Evidence("tampering-proof.json", JsonSerializer.Serialize(new { status = (int)response.StatusCode, databaseRows = 0 }));
    }

    [Fact]
    public async Task S02_AnotherSensorIdIsForbidden()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload() with { SensorId = "DVC-002" }));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task S03_DeviceWithoutWriteScopeIsForbidden()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload() with { SensorId = "DVC-003" }, device: "DVC-003"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task S04_ReplayOfNonceIsUnauthorized()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        string nonce = Guid.NewGuid().ToString("N"); var payload = ClimateFactory.Payload();
        (await client.SendAsync(ClimateFactory.Signed(payload, nonce: nonce))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.SendAsync(ClimateFactory.Signed(payload, nonce: nonce))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task S05_ExpiredEnvelopeIsUnauthorized()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload(), timestamp: DateTimeOffset.UtcNow.AddMinutes(-3).ToUnixTimeSeconds()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(85, 100)]
    public async Task I03_PhysicalBoundariesAreAccepted(double t, double h)
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload() with { TemperatureC = (decimal)t, HumidityPercent = (decimal)h }));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(-40.01, 45)]
    [InlineData(85.01, 45)]
    [InlineData(23.5, -0.01)]
    [InlineData(23.5, 100.01)]
    public async Task I04_OutOfRangeIsBadRequest(double t, double h)
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload() with { TemperatureC = (decimal)t, HumidityPercent = (decimal)h }));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task I05_InvalidJsonIsBadRequest()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var response = await client.SendAsync(ClimateFactory.SignedRaw("{broken"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task I06_DuplicateSequenceIsConflict()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var p = ClimateFactory.Payload();
        (await client.SendAsync(ClimateFactory.Signed(p))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.SendAsync(ClimateFactory.Signed(p))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task S06_FifthRequestIsRateLimited()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var statuses = new List<int>();
        for (int i = 1; i <= 5; i++)
            statuses.Add((int)(await client.SendAsync(ClimateFactory.Signed(ClimateFactory.Payload(i)))).StatusCode);
        statuses.Should().Equal(201, 201, 201, 201, 429);
        Evidence("rate-limit-proof.json", JsonSerializer.Serialize(statuses));
    }

    [Fact]
    public async Task S07_OversizedBodyIsRejected()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        (await client.SendAsync(ClimateFactory.SignedRaw(new string('a', 5000)))).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task S08_InvalidHexIsUnauthorized()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var req = ClimateFactory.Signed(ClimateFactory.Payload());
        req.Headers.Remove("X-Signature"); req.Headers.Add("X-Signature", new string('z', 64));
        (await client.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task S09_AuditContainsDecisionButNoSecrets()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        var req = ClimateFactory.Signed(ClimateFactory.Payload());
        string signature = req.Headers.GetValues("X-Signature").Single();
        (await client.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Created);
        var events = factory.Services.GetRequiredService<AuditSink>().Events;
        events.Should().Contain(x => x.Outcome == "ACCEPTED");
        string json = JsonSerializer.Serialize(events);
        json.Should().NotContain(signature).And.NotContain(Convert.ToBase64String(TestRegistry.TestKey));
        Evidence("audit-example.json", json);
    }

    [Fact]
    public async Task I07_EfCoreQueryUsesBoundParameter()
    {
        using var factory = new ClimateFactory(); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClimateDbContext>();
        string sensor = "DVC-001";
        string sql = db.Telemetry.Where(x => x.SensorId == sensor).ToQueryString();
        sql.Should().Contain("@__sensor");
        Evidence("parameterized-query.sql", sql);
        (await db.Telemetry.Where(x => x.SensorId == sensor).CountAsync()).Should().Be(0);
    }

    private static void Evidence(string name, string content)
    {
        string? path = Environment.GetEnvironmentVariable("CLIMATE_EVIDENCE");
        if (path is not null) { Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, name), content); }
    }
}
