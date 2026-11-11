using System.Globalization;
using System.Net;
using System.Text;
using Climate.Core;

namespace Climate.Operations;

/// <summary>Resigns each retry with fresh timestamp and nonce; the event sequence stays unchanged.</summary>
public sealed class HmacOutboxTransport(HttpClient client, string deviceId, byte[] key) : IOutboxTransport
{
    public async Task<DeliveryOutcome> SendAsync(string payload, CancellationToken ct)
    {
        const string path = "/api/v1/telemetry";
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        string stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string nonce = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("X-Device-Id", deviceId); request.Headers.Add("X-Key-Id", "key-1");
        request.Headers.Add("X-Timestamp", stamp); request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Signature", HmacProtocol.Sign(key,
            HmacProtocol.Canonical("POST", path, deviceId, "key-1", stamp, nonce, bytes)));
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Created) return DeliveryOutcome.Accepted;
        if (response.StatusCode == HttpStatusCode.Conflict) return DeliveryOutcome.Conflict;
        if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Transient delivery failure: " + (int)response.StatusCode);
        return DeliveryOutcome.Invalid;
    }
}
