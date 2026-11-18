namespace NetDaemonApps;

public readonly record struct ElectricityPriceInterval(DateTimeOffset Start, DateTimeOffset End, decimal Price);

public readonly record struct ElectricityPriceWindow(DateTimeOffset Start, DateTimeOffset End, decimal AveragePrice);

public readonly record struct ElectricityPricePeriod(DateTimeOffset Start, DateTimeOffset End);

public static class ElectricityPricePolicy
{
    public static readonly TimeSpan Quarter = TimeSpan.FromMinutes(15);

    public static IReadOnlyList<ElectricityPriceWindow> FindCheapWindows(
        IEnumerable<IReadOnlyList<ElectricityPriceInterval>> days,
        TimeSpan minimumDuration,
        decimal absoluteLimit,
        decimal minimumDiscount)
    {
        var windows = new List<ElectricityPriceWindow>();
        var start = default(DateTimeOffset);
        var end = default(DateTimeOffset);
        decimal priceSum = 0;
        var count = 0;

        foreach (var day in days)
        {
            var limit = Math.Max(absoluteLimit, day.Average(quarter => quarter.Price) - minimumDiscount);
            foreach (var quarter in day)
            {
                if (quarter.Start != end || quarter.Price > limit)
                {
                    FinishWindow();
                }

                if (quarter.Price > limit)
                {
                    continue;
                }

                if (count == 0)
                {
                    start = quarter.Start;
                }

                end = quarter.End;
                priceSum += quarter.Price;
                count++;
            }
        }

        FinishWindow();
        return windows;

        void FinishWindow()
        {
            if (count > 0 && end - start >= minimumDuration)
            {
                windows.Add(new ElectricityPriceWindow(start, end, priceSum / count));
            }

            count = 0;
            priceSum = 0;
        }
    }

    public static IReadOnlyList<ElectricityPricePeriod> GetPeriodsInRange(
        IEnumerable<ElectricityPriceWindow> windows,
        DateTimeOffset from,
        DateTimeOffset until,
        TimeSpan minimumDuration)
    {
        var periods = new List<ElectricityPricePeriod>();
        foreach (var window in windows)
        {
            var start = window.Start > from ? window.Start : from;
            var end = window.End < until ? window.End : until;
            if (end - start >= minimumDuration)
            {
                periods.Add(new ElectricityPricePeriod(start, end));
            }
        }

        return periods;
    }

    public static ElectricityPriceWindow? FindNightTip(
        IReadOnlyList<ElectricityPriceInterval> eveningPrices,
        IReadOnlyList<ElectricityPriceInterval> nightPrices,
        DateTimeOffset eveningStart,
        DateTimeOffset midnight,
        DateTimeOffset nightEnd,
        TimeSpan duration,
        decimal minimumSavings)
    {
        var evening = FindCheapestWindow(eveningPrices, eveningStart, midnight, duration);
        var night = FindCheapestWindow(nightPrices, midnight, nightEnd, duration);

        return evening is { } comparedTo && night is { } candidate &&
               comparedTo.AveragePrice - candidate.AveragePrice >= minimumSavings
            ? candidate
            : null;
    }

    private static ElectricityPriceWindow? FindCheapestWindow(
        IReadOnlyList<ElectricityPriceInterval> prices,
        DateTimeOffset from,
        DateTimeOffset until,
        TimeSpan duration)
    {
        ElectricityPriceWindow? best = null;
        var quarterCount = (int)(duration / Quarter);
        for (var first = 0; first + quarterCount <= prices.Count; first++)
        {
            var start = prices[first].Start;
            var end = prices[first + quarterCount - 1].End;
            if (start < from || end > until || end - start != duration)
            {
                continue;
            }

            var sum = 0m;
            for (var index = first; index < first + quarterCount; index++)
            {
                sum += prices[index].Price;
            }

            var average = sum / quarterCount;
            if (best is null || average < best.Value.AveragePrice)
            {
                best = new ElectricityPriceWindow(start, end, average);
            }
        }

        return best;
    }
}
