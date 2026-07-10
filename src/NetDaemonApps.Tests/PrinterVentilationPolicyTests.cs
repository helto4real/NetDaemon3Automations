using FluentAssertions;

namespace NetDaemonApps.Tests;

public sealed class PrinterVentilationPolicyTests
{
    [Theory]
    [InlineData("running", "Generic PLA", 20)]
    [InlineData("running", "Bambu ASA", 75)]
    [InlineData("running", "ABS black", 75)]
    [InlineData("idle", "ABS black", null)]
    public void GetPrinterFanPercentage_ShouldRequireActivePrint(
        string printStatus,
        string filament,
        int? expected)
    {
        PrinterVentilationPolicy.GetPrinterFanPercentage(printStatus, filament).Should().Be(expected);
    }
}
