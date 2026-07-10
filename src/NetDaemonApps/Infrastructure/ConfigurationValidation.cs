namespace NetDaemonApps;

internal static class ConfigurationValidation
{
    public static void DailyTime(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(name, value, "Time of day must be between 00:00:00 and 23:59:59.");
        }
    }

    public static void Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, value, "Duration must be greater than zero.");
        }
    }

    public static void NonNegative(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, value, "Duration must not be negative.");
        }
    }

    public static void Finite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite.");
        }
    }
}
