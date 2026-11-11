using Climate.Core;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace Climate.Operations;

public enum DeliveryOutcome { Accepted, Conflict, Invalid }
public interface IOutboxTransport
{
    Task<DeliveryOutcome> SendAsync(string payload, CancellationToken ct);
}

/// <summary>Single worker outbox. No record is removed on a failed delivery.</summary>
public sealed class OfflineBuffer(OutboxContext db, IOutboxTransport transport, TimeSpan retryDelay)
{
    private readonly ResiliencePipeline<DeliveryOutcome> _pipeline = new ResiliencePipelineBuilder<DeliveryOutcome>()
        .AddRetry(new RetryStrategyOptions<DeliveryOutcome>
        {
            MaxRetryAttempts = 2, Delay = retryDelay, BackoffType = DelayBackoffType.Exponential,
            UseJitter = false, ShouldHandle = new PredicateBuilder<DeliveryOutcome>().Handle<HttpRequestException>()
        }).Build();

    public async Task<Guid> EnqueueAsync(TelemetryRequest payload, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var existing = await db.Messages.SingleOrDefaultAsync(x => x.SensorId == payload.SensorId && x.Sequence == payload.Sequence, ct);
        if (existing is not null)
        {
            if (existing.Payload != json) throw new InvalidOperationException("Conflicting event key.");
            return existing.Id;
        }
        var message = new OutboxMessage { Id = Guid.NewGuid(), SensorId = payload.SensorId,
            Sequence = payload.Sequence, Payload = json, CreatedAtUtc = DateTime.UtcNow };
        db.Messages.Add(message); await db.SaveChangesAsync(ct); return message.Id;
    }

    public async Task<DeliveryState> DeliverAsync(Guid id, CancellationToken ct = default)
    {
        var message = await db.Messages.SingleAsync(x => x.Id == id, ct);
        if (message.State != DeliveryState.Pending) return message.State;
        try
        {
            var outcome = await _pipeline.ExecuteAsync(async token =>
            {
                message.Attempts++; await db.SaveChangesAsync(token);
                return await transport.SendAsync(message.Payload, token);
            }, ct);
            message.State = outcome == DeliveryOutcome.Accepted ? DeliveryState.Delivered : DeliveryState.NeedsReview;
        }
        catch (HttpRequestException) { message.State = DeliveryState.Pending; }
        await db.SaveChangesAsync(ct); return message.State;
    }
}
