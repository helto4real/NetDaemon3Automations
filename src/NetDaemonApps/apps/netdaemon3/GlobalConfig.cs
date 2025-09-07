/// <summary>
/// Global configuration record for time settings.
/// </summary>
public record GlobalConfig
{
    /// <summary>
    /// Gets the morning time.
    /// </summary>
    public TimeSpan MorningTime { get; init; }
    /// <summary>
    /// Gets the day time.
    /// </summary>
    public TimeSpan DayTime { get; init; }
    /// <summary>
    /// Gets the night time for weekends.
    /// </summary>
    public TimeSpan NightTimeWeekends { get; init; }
    /// <summary>
    /// Gets the night time for weekdays.
    /// </summary>
    public TimeSpan NightTimeWeekdays { get; init; }
}


/// <summary>
/// Test application for configuration.
/// </summary>
// [NetDaemonApp]
// [Focus]
public class ConfigTestApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigTestApp"/> class.
    /// </summary>
    /// <param name="config">The global configuration.</param>
    public ConfigTestApp(IAppConfig<GlobalConfig> config)
    {
        Console.WriteLine(config.Value.DayTime);
    }

}
