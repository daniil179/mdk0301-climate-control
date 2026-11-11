namespace Climate.Core;

/// <summary>Untrusted measurement submitted by an enrolled climate sensor.</summary>
public sealed record TelemetryRequest(
    string SensorId,
    long Sequence,
    decimal TemperatureC,
    decimal HumidityPercent,
    string MeasuredAt);

/// <summary>A trusted device entry supplied by server configuration.</summary>
public sealed record DeviceIdentity(string SensorId, string RoomId, byte[] Key, bool CanWrite);

/// <summary>Resolves a device key without trusting request payload permissions.</summary>
public interface IDeviceRegistry
{
    DeviceIdentity? Find(string sensorId, string keyId);
}

/// <summary>A bounded audit event without keys, signatures or raw payloads.</summary>
public sealed record AuditEvent(string SensorId, string Outcome, DateTimeOffset ServerTime);

/// <summary>Records operational decisions; persistence is supplied by infrastructure.</summary>
public interface IAuditSink
{
    void Record(string sensorId, string outcome);
}
