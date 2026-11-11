using System.Collections.Concurrent;
using Climate.Core;

namespace Climate.Api.Security;

/// <summary>Bounded in-process audit for tests; production requires protected persistent forwarding.</summary>
public sealed class AuditSink(TimeProvider clock) : IAuditSink
{
    public ConcurrentQueue<AuditEvent> Events { get; } = new();

    public void Record(string sensorId, string outcome)
    {
        Events.Enqueue(new AuditEvent(sensorId, outcome, clock.GetUtcNow()));
        while (Events.Count > 2000)
            Events.TryDequeue(out _);
    }
}
