namespace NetDaemonApps;

public enum HouseMode
{
    Morning,
    Day,
    Evening,
    Night,
    Cleaning,
}

public static class HouseModeCodec
{
    public const string Morning = "Morgon";
    public const string Day = "Dag";
    public const string Evening = "Kväll";
    public const string Night = "Natt";
    public const string Cleaning = "Städning";

    public static string Format(HouseMode mode) => mode switch
    {
        HouseMode.Morning => Morning,
        HouseMode.Day => Day,
        HouseMode.Evening => Evening,
        HouseMode.Night => Night,
        HouseMode.Cleaning => Cleaning,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported house mode"),
    };

    public static bool TryParse(string? value, out HouseMode mode)
    {
        mode = value switch
        {
            Morning => HouseMode.Morning,
            Day => HouseMode.Day,
            Evening => HouseMode.Evening,
            Night => HouseMode.Night,
            Cleaning => HouseMode.Cleaning,
            _ => default,
        };

        return value is Morning or Day or Evening or Night or Cleaning;
    }
}
