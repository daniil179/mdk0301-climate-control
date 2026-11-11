using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Climate.Core;

/// <summary>Checks syntax, physical ranges and the accepted measurement age.</summary>
public sealed class TelemetryValidator : AbstractValidator<TelemetryRequest>
{
    /// <summary>Creates deterministic rules using the injected server clock.</summary>
    public TelemetryValidator(TimeProvider clock)
    {
        RuleFor(x => x.SensorId).NotEmpty().Matches(@"\ADVC-[0-9]{3}\z");
        RuleFor(x => x.Sequence).InclusiveBetween(1L, 1_000_000_000L);
        RuleFor(x => x.TemperatureC).InclusiveBetween(-40m, 85m);
        RuleFor(x => x.HumidityPercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.MeasuredAt).NotEmpty().MaximumLength(40)
            .Must(value => IsFresh(value, clock.GetUtcNow()));
    }

    /// <summary>Requires an explicit offset and a time in [-10 minutes, +30 seconds].</summary>
    public static bool IsFresh(string? value, DateTimeOffset now)
    {
        if (value is null || !Regex.IsMatch(value,
            @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})\z"))
            return false;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var measured))
            return false;
        return measured >= now.AddMinutes(-10) && measured <= now.AddSeconds(30);
    }
}
