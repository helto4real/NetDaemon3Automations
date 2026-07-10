using FluentAssertions;

namespace NetDaemonApps.Tests;

public sealed class RandomSelectionTests
{
    [Fact]
    public void SelectRandom_ShouldAllowLastValue()
    {
        var values = new[] { "first", "last" };

        var selected = RandomSelection.SelectRandom(values, new LastValueRandom());

        selected.Should().Be("last");
    }

    private sealed class LastValueRandom : Random
    {
        public override int Next(int maxValue) => maxValue - 1;
    }
}
