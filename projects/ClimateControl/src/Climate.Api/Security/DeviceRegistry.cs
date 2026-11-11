using Climate.Core;

namespace Climate.Api.Security;

/// <summary>Loads enrollment data from external configuration, never from telemetry.</summary>
public sealed class DeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<string, DeviceIdentity> _devices = new(StringComparer.Ordinal);

    public DeviceRegistry(IConfiguration configuration)
    {
        foreach (var item in configuration.GetSection("Climate:Devices").GetChildren())
        {
            string id = item["SensorId"] ?? "";
            string keyId = item["KeyId"] ?? "";
            byte[] key = Convert.FromBase64String(item["KeyBase64"] ?? "");
            if (key.Length < 32 || id.Length == 0 || keyId.Length == 0)
                throw new InvalidOperationException("Invalid device enrollment configuration.");
            _devices.Add(id + "/" + keyId, new DeviceIdentity(id,
                item["RoomId"] ?? "", key, bool.Parse(item["CanWrite"] ?? "false")));
        }
    }

    public DeviceIdentity? Find(string sensorId, string keyId) =>
        _devices.GetValueOrDefault(sensorId + "/" + keyId);
}
