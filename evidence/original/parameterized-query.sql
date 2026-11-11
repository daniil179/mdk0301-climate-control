.param set @__sensor_0 'DVC-001'

SELECT "t"."Id", "t"."HumidityPercent", "t"."MeasuredAt", "t"."RoomId", "t"."SensorId", "t"."Sequence", "t"."TemperatureC"
FROM "Telemetry" AS "t"
WHERE "t"."SensorId" = @__sensor_0