public record GlobalConfig
{
    // Time of day configs
    public TimeSpan MorningTime { get; init; }
    public TimeSpan DayTime { get; init; }
    public TimeSpan NightTimeWeekends { get; init; }
    public TimeSpan NightTimeWeekdays { get; init; }
}


[NetDaemonApp]
[Focus]
public class ConfigTestApp
{
    public ConfigTestApp(IAppConfig<GlobalConfig> config)
    {
        Console.WriteLine(config.Value.DayTime);
    }

}
