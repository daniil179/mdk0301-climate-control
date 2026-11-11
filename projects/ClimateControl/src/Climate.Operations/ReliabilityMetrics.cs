namespace Climate.Operations;

public sealed record ReliabilityResult(double ObservationHours, double DowntimeMinutes, int Failures,
    double? MtbfHours, double? MttrMinutes, double AvailabilityPercent);
public static class ReliabilityMetrics
{
    public static ReliabilityResult Calculate(double observationHours, params double[] downtimeMinutes)
    {
        if (observationHours <= 0 || downtimeMinutes.Any(x => x < 0) || downtimeMinutes.Sum() > observationHours * 60)
            throw new ArgumentOutOfRangeException(nameof(observationHours));
        double down = downtimeMinutes.Sum();int n = downtimeMinutes.Length;
        double uptime = observationHours - down / 60;
        return new(observationHours, down, n, n == 0 ? null : uptime / n,
            n == 0 ? null : down / n, uptime / observationHours * 100);
    }
}
