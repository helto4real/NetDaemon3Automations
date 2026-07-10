using FluentAssertions;

namespace NetDaemonApps.Tests;

public sealed class TimeWindowPolicyTests
{
    [Theory]
    [InlineData(6, 5, 10, true)]
    [InlineData(4, 5, 10, false)]
    [InlineData(23, 22, 6, true)]
    [InlineData(3, 22, 6, true)]
    [InlineData(12, 22, 6, false)]
    public void IsTimeBetween_ShouldHandleSameDayAndOvernightWindows(
        int currentHour,
        int startHour,
        int endHour,
        bool expected)
    {
        var result = TimeWindowPolicy.IsTimeBetween(
            new TimeOnly(currentHour, 0),
            new TimeOnly(startHour, 0),
            new TimeOnly(endHour, 0));

        result.Should().Be(expected);
    }
}
