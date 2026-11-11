using System.Text;
using Climate.Core;
using Climate.Before;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Climate.UnitTests;

public sealed class FixedClock : TimeProvider
{
    public static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class ValidationTests
{
    private readonly TelemetryValidator _validator = new(new FixedClock());
    private static TelemetryRequest Valid => new("DVC-001", 1, 23.5m, 45m, "2026-11-04T09:00:00Z");

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(85, 100)]
    [InlineData(23.5, 45)]
    public void U01_ValidRanges(double t, double h) =>
        _validator.Validate(Valid with { TemperatureC = (decimal)t, HumidityPercent = (decimal)h }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(-40.01)]
    [InlineData(85.01)]
    public void U02_InvalidTemperature(double t) =>
        _validator.Validate(Valid with { TemperatureC = (decimal)t }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void U03_InvalidHumidity(double h) =>
        _validator.Validate(Valid with { HumidityPercent = (decimal)h }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(1000000001)]
    public void U04_InvalidSequence(long n) =>
        _validator.Validate(Valid with { Sequence = n }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("DVC-001\n")]
    [InlineData("sensor-1")]
    [InlineData("")]
    public void U05_InvalidSensor(string id) =>
        _validator.Validate(Valid with { SensorId = id }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("2026-11-04T08:50:00Z", true)]
    [InlineData("2026-11-04T09:00:30Z", true)]
    [InlineData("2026-11-04T08:49:59Z", false)]
    [InlineData("2026-11-04T09:00:31Z", false)]
    [InlineData("2026-11-04T09:00:00", false)]
    public void U06_TimeBoundaries(string at, bool expected) =>
        _validator.Validate(Valid with { MeasuredAt = at }).IsValid.Should().Be(expected);

    [Fact]
    public void U07_BodyTamperingChangesHmac()
    {
        byte[] key = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        byte[] first = HmacProtocol.Canonical("POST", "/api/v1/telemetry", "DVC-001", "key-1", "1", "n", Encoding.UTF8.GetBytes("23.5"));
        byte[] changed = HmacProtocol.Canonical("POST", "/api/v1/telemetry", "DVC-001", "key-1", "1", "n", Encoding.UTF8.GetBytes("83.5"));
        HmacProtocol.Verify(key, changed, HmacProtocol.Sign(key, first)).Should().BeFalse();
    }

    [Theory]
    [InlineData(-120, true)]
    [InlineData(120, true)]
    [InlineData(-121, false)]
    [InlineData(121, false)]
    public void U08_EnvelopeWindow(int delta, bool expected) =>
        HmacProtocol.IsFresh(FixedClock.Now.AddSeconds(delta).ToUnixTimeSeconds().ToString(), FixedClock.Now).Should().Be(expected);

    [Fact]
    public void U09_MalformedHexRejected() =>
        HmacProtocol.Verify(new byte[32], new byte[1], new string('z', 64)).Should().BeFalse();

    [Theory]
    [InlineData(-100)]
    [InlineData(100)]
    public void B01_ReproducesLegacyRangeDefect(decimal t)
    {
        var controller = new LegacyClimateController();
        controller.send("DVC-001", t, 250m, 1).Should().BeOfType<OkResult>();
    }
}
