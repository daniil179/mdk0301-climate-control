using System.Security.Claims;
using Climate.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Climate.Api.Controllers;

/// <summary>Versioned read contracts. Version 1 stays flat; version 2 adds an explicit unit.</summary>
[ApiController]
[Authorize(Policy = "TelemetryReader")]
public sealed class ReadingsController(ClimateDbContext db) : ControllerBase
{
    [HttpGet("api/v1/climate/readings/latest/{sensorId}")]
    public async Task<IActionResult> GetV1(string sensorId, CancellationToken ct)
    {
        if (sensorId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return Forbid();
        var row = await db.Telemetry.Where(x => x.SensorId == sensorId)
            .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
        return row is null ? NotFound() : Ok(new
        {
            sensorId = row.SensorId, temperatureC = row.TemperatureC,
            humidityPercent = row.HumidityPercent
        });
    }

    [HttpGet("api/v2/climate/readings/latest/{sensorId}")]
    public async Task<IActionResult> GetV2(string sensorId, CancellationToken ct)
    {
        if (sensorId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return Forbid();
        var row = await db.Telemetry.Where(x => x.SensorId == sensorId)
            .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
        return row is null ? NotFound() : Ok(new
        {
            sensorId = row.SensorId,
            temperature = new { value = row.TemperatureC, unit = "Cel" },
            humidityPercent = row.HumidityPercent, measuredAt = row.MeasuredAt,
            quality = "measured"
        });
    }
}
