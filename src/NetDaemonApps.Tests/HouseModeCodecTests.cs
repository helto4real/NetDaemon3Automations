using FluentAssertions;

namespace NetDaemonApps.Tests;

public sealed class HouseModeCodecTests
{
    [Theory]
    [InlineData(HouseMode.Morning, "Morgon")]
    [InlineData(HouseMode.Day, "Dag")]
    [InlineData(HouseMode.Evening, "Kväll")]
    [InlineData(HouseMode.Night, "Natt")]
    [InlineData(HouseMode.Cleaning, "Städning")]
    public void Format_ShouldUseConfiguredHomeAssistantVocabulary(HouseMode mode, string expected)
    {
        HouseModeCodec.Format(mode).Should().Be(expected);
    }
}
