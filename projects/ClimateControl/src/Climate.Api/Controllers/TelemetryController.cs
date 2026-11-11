using System.Globalization;
using System.Security.Claims;
using Climate.Core;
using Climate.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Climate.Api.Controllers;

/// <summary>Accepts authenticated measurements and enforces device ownership.</summary>
[ApiController]
[Route("api/v1/telemetry")]
[Authorize(Policy = "TelemetryWriter")]
[EnableRateLimiting("telemetry")]
public sealed class TelemetryController(
    ClimateDbContext db, IValidator<TelemetryRequest> validator, IAuditSink audit) : ControllerBase
{
    /// <summary>Validates and stores one signed measurement; duplicate sequence returns conflict.</summary>
    /// <param name="request">Untrusted sensor data.</param>
    /// <param name="cancellationToken">Cancellation propagated to relational storage.</param>
    /// <returns>201 on insertion, 400 on invalid data, 403 on identity mismatch, 409 on duplicate.</returns>
    [HttpPost]
    public async Task<IActionResult> Post(TelemetryRequest request, CancellationToken cancellationToken)
    {
        string sensor = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (request.SensorId != sensor)
        {
            audit.Record(sensor, "OWNERSHIP_DENIED");
            return Forbid();
        }
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            audit.Record(sensor, "VALIDATION_DENIED");
            return BadRequest(new { code = "VALIDATION_ERROR", fields = result.Errors.Select(x => x.PropertyName).Distinct() });
        }
        var row = new TelemetryRow
        {
            SensorId = sensor, RoomId = User.FindFirstValue("room")!, Sequence = request.Sequence,
            TemperatureC = request.TemperatureC, HumidityPercent = request.HumidityPercent,
            MeasuredAt = DateTimeOffset.Parse(request.MeasuredAt, CultureInfo.InvariantCulture).ToUniversalTime()
        };
        db.Telemetry.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        {
            audit.Record(sensor, "DUPLICATE_SEQUENCE");
            return Conflict(new { code = "DUPLICATE_SEQUENCE" });
        }
        audit.Record(sensor, "ACCEPTED");
        return StatusCode(StatusCodes.Status201Created, new { id = row.Id, sensorId = row.SensorId, roomId = row.RoomId });
    }
}
