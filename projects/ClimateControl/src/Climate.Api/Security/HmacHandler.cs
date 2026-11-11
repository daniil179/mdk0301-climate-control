using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Climate.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Climate.Api.Security;

/// <summary>Authenticates an enrolled device using the complete signed request.</summary>
public sealed class HmacHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IDeviceRegistry registry,
    ReplayGuard replay, TimeProvider clock, IAuditSink audit)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string device = Request.Headers["X-Device-Id"].ToString();
        string keyId = Request.Headers["X-Key-Id"].ToString();
        string stamp = Request.Headers["X-Timestamp"].ToString();
        string nonce = Request.Headers["X-Nonce"].ToString();
        string signature = Request.Headers["X-Signature"].ToString();
        if (!Regex.IsMatch(device, @"\ADVC-[0-9]{3}\z") ||
            !Regex.IsMatch(keyId, @"\Akey-[0-9]{1,3}\z") ||
            !Guid.TryParseExact(nonce, "N", out _) || Request.QueryString.HasValue ||
            !HmacProtocol.IsFresh(stamp, clock.GetUtcNow()))
            return Reject("unknown", "AUTH_FORMAT");
        DeviceIdentity? identity = registry.Find(device, keyId);
        if (identity is null)
            return Reject(device, "AUTH_DEVICE");
        Request.EnableBuffering(bufferThreshold: 4096, bufferLimit: 4096);
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, Context.RequestAborted);
        Request.Body.Position = 0;
        byte[] canonical = HmacProtocol.Canonical(Request.Method,
            Request.PathBase + Request.Path, device, keyId, stamp, nonce, body.ToArray());
        if (!HmacProtocol.Verify(identity.Key, canonical, signature))
            return Reject(device, "AUTH_SIGNATURE");
        if (!replay.TryUse(device + "/" + keyId + "/" + nonce))
            return Reject(device, "AUTH_REPLAY");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, device), new("room", identity.RoomId)
        };
        if (identity.CanWrite)
            claims.Add(new Claim("scope", "telemetry:write"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private AuthenticateResult Reject(string device, string reason)
    {
        audit.Record(device, reason);
        return AuthenticateResult.Fail("Device authentication failed.");
    }
}
