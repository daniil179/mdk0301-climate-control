using System.Threading.RateLimiting;
using Climate.Api.Security;
using Climate.Core;
using Climate.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
builder.Services.AddSingleton<ReplayGuard>();
builder.Services.AddSingleton<AuditSink>();
builder.Services.AddSingleton<IAuditSink>(sp => sp.GetRequiredService<AuditSink>());
builder.Services.AddScoped<IValidator<TelemetryRequest>, TelemetryValidator>();
builder.Services.AddDbContext<ClimateDbContext>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("Climate") ?? "Data Source=climate.db"));
builder.Services.AddAuthentication("DeviceHmac")
    .AddScheme<AuthenticationSchemeOptions, HmacHandler>("DeviceHmac", _ => { });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("TelemetryWriter", p => p.RequireAuthenticatedUser().RequireClaim("scope", "telemetry:write"));
    o.AddPolicy("TelemetryReader", p => p.RequireAuthenticatedUser());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("telemetry", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Climate:RatePermitLimit", 4),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR" });
}));
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > 4096)
    {
        context.Response.StatusCode = 413;
        return;
    }
    try { await next(); }
    catch (IOException)
    {
        context.Response.StatusCode = 413;
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok", module = "Climate Control" })).AllowAnonymous();
app.MapControllers();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ClimateDbContext>().Database.EnsureCreated();
app.Run();

/// <summary>Public entry point for the integration test host.</summary>
public partial class Program { }
