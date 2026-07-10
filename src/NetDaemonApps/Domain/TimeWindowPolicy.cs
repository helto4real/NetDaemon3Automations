namespace NetDaemonApps;

public static class TimeWindowPolicy
{
    public static bool IsTimeBetween(TimeOnly now, TimeOnly start, TimeOnly end) =>
        start <= end
            ? now >= start && now <= end
            : now >= start || now <= end;
}
