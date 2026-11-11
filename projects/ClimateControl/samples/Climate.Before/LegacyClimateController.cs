using Microsoft.AspNetCore.Mvc;

namespace Climate.Before;

// Intentionally flawed educational specimen. Never mapped by the secure API.
public sealed class LegacyClimateController : ControllerBase
{
    private readonly List<LegacyReading> data = new();
    private readonly List<string> logs = new();

    public IActionResult send(string id, decimal t, decimal h, int seq)
    {
        if (id == null)
            return BadRequest();
        if (id == "")
            return BadRequest();
        if (seq < 1)
            return BadRequest();
        if (t >= -40 || t <= 85)
        {
            if (h > 70)
                logs.Add("humidity high");
            if (h < 10)
                logs.Add("humidity low");
            data.Add(new LegacyReading(id, t, h, seq));
            if (t > 28)
                logs.Add("temperature high");
            if (t < 16)
                logs.Add("temperature low");
            return Ok();
        }
        return BadRequest();
    }

    public int count()
    {
        if (data.Count == 0)
            return 0;
        return data.Count;
    }

    public string report()
    {
        if (logs.Count == 0)
            return "none";
        return string.Join(";", logs);
    }
}

public sealed record LegacyReading(string Id, decimal Temperature, decimal Humidity, int Sequence);
