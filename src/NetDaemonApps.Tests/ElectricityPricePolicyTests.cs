using System.Linq;
using FluentAssertions;

namespace NetDaemonApps.Tests;

public sealed class ElectricityPricePolicyTests
{
    private static readonly DateTimeOffset Midnight = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FindCheapWindows_ShouldIncludeWholeDay_WhenSpotPriceIsAlwaysLow()
    {
        var prices = Quarters(Midnight, 96, _ => 0.30m);

        var windows = ElectricityPricePolicy.FindCheapWindows([prices], TimeSpan.FromHours(2), 0.30m, 0.20m);

        windows.Should().Equal(new ElectricityPriceWindow(Midnight, Midnight.AddDays(1), 0.30m));
    }

    [Fact]
    public void FindCheapWindows_ShouldIgnoreShortSpikesAndJoinCheapQuartersAcrossMidnight()
    {
        var firstDay = Quarters(Midnight.AddDays(-1), 96, index => index is 48 or >= 92 ? 0.70m : 1.30m);
        var secondDay = Quarters(Midnight, 96, index => index < 4 ? 0.50m : 1.30m);

        var windows = ElectricityPricePolicy.FindCheapWindows([firstDay, secondDay], TimeSpan.FromHours(2), 0.30m, 0.20m);

        windows.Should().Equal(new ElectricityPriceWindow(Midnight.AddHours(-1), Midnight.AddHours(1), 0.60m));
    }

    [Theory]
    [InlineData(25, false)]
    [InlineData(30, true)]
    public void FindNightTip_ShouldRequireConfiguredSavingsComparedWithCheapestEveningWindow(
        int savingsInOre, bool expectsTip)
    {
        var evening = Quarters(Midnight.AddHours(-5), 20, index => index < 8 ? 1m : 2m);
        var night = Quarters(Midnight, 24, index => index is >= 8 and < 16 ? 1m - savingsInOre / 100m : 2m);

        var tip = ElectricityPricePolicy.FindNightTip(evening, night, Midnight.AddHours(-5), Midnight,
            Midnight.AddHours(6), TimeSpan.FromHours(2), 0.30m);

        if (expectsTip)
        {
            tip.Should().Be(new ElectricityPriceWindow(Midnight.AddHours(2), Midnight.AddHours(4), 0.70m));
        }
        else
        {
            tip.Should().BeNull();
        }
    }

    private static ElectricityPriceInterval[] Quarters(DateTimeOffset start, int count, Func<int, decimal> price) =>
        Enumerable.Range(0, count)
            .Select(index => new ElectricityPriceInterval(start.AddMinutes(index * 15), start.AddMinutes((index + 1) * 15), price(index)))
            .ToArray();
}
