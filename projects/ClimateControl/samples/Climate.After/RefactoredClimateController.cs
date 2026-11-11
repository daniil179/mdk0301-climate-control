using Microsoft.AspNetCore.Mvc;

namespace Climate.After;

/// <summary>Small controller after extracting validation, storage and diagnostics.</summary>
public sealed class RefactoredClimateController : ControllerBase
{
    private readonly IReadingService _service;
    public RefactoredClimateController(IReadingService service) => _service = service;
    /// <summary>Delegates processing without owning storage or diagnostic collections.</summary>
    public IActionResult Send(string id, decimal temperature, decimal humidity, int sequence)
    {
        bool accepted = _service.Accept(id, temperature, humidity, sequence);
        return accepted ? Ok() : BadRequest();
    }
}

/// <summary>Port used by the controller.</summary>
public interface IReadingService
{
    bool Accept(string id, decimal temperature, decimal humidity, int sequence);
}

/// <summary>Owns the measurement collection only.</summary>
public sealed class ReadingStore
{
    private readonly List<Reading> _readings = new();
    public int Count => _readings.Count;
    public void Add(Reading reading) => _readings.Add(reading);
}

/// <summary>Owns diagnostics only.</summary>
public sealed class DiagnosticLog
{
    private readonly List<string> _messages = new();
    public string Report => _messages.Count == 0 ? "none" : string.Join(";", _messages);
    public void Record(string message) => _messages.Add(message);
}

/// <summary>Typed measurement for the refactoring example.</summary>
public sealed record Reading(string SensorId, decimal Temperature, decimal Humidity, int Sequence);

/// <summary>Validates physical bounds before storing and recording diagnostics.</summary>
public sealed class ReadingService : IReadingService
{
    private readonly ReadingStore _store;
    private readonly DiagnosticLog _log;
    public ReadingService(ReadingStore store, DiagnosticLog log)
    {
        _store = store;
        _log = log;
    }
    public bool Accept(string id, decimal temperature, decimal humidity, int sequence)
    {
        if (!ReadingRules.IsValid(id, temperature, humidity, sequence))
            return false;
        var reading = new Reading(id, temperature, humidity, sequence);
        _store.Add(reading);
        ReadingDiagnostics.Record(reading, _log);
        return true;
    }
}

/// <summary>Validation extracted from transport and storage.</summary>
public static class ReadingRules
{
    public static bool IsValid(string id, decimal t, decimal h, int seq) =>
        !string.IsNullOrEmpty(id) && seq >= 1 && t >= -40 && t <= 85 && h >= 0 && h <= 100;
}

/// <summary>Independent diagnostic rules; each condition has an explicit meaning.</summary>
public static class ReadingDiagnostics
{
    public static void Record(Reading reading, DiagnosticLog log)
    {
        if (reading.Humidity > 70) log.Record("humidity high");
        if (reading.Humidity < 10) log.Record("humidity low");
        if (reading.Temperature > 28) log.Record("temperature high");
        if (reading.Temperature < 16) log.Record("temperature low");
    }
}
