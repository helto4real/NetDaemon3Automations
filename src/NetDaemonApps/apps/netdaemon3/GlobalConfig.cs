namespace NetDaemonApps;

public sealed record GlobalConfig
{
    public TimeSpan MorningTime { get; init; } = new(5, 15, 0);

    public TimeSpan DayTime { get; init; } = new(9, 0, 0);

    public TimeSpan EveningCheckTime { get; init; } = new(15, 14, 0);

    public TimeSpan NightTimeWeekends { get; init; } = new(23, 40, 0);

    public TimeSpan NightTimeWeekdays { get; init; } = new(22, 40, 0);

    public double EveningIlluminanceThreshold { get; init; } = 20;

    public double MorningIlluminanceThreshold { get; init; } = 35;

    public TimeSpan IlluminanceStabilityDuration { get; init; } = TimeSpan.FromMinutes(15);
}
