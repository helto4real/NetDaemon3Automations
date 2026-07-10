namespace NetDaemonApps;

public static class PrinterVentilationPolicy
{
    public static int? GetPrinterFanPercentage(string? printStatus, string? filament) =>
        printStatus != HomeAssistantStates.Running
            ? null
            : ContainsHazardousFilament(filament) ? 75 : 20;

    public static bool ContainsHazardousFilament(string? filament) =>
        filament?.Contains("asa", StringComparison.OrdinalIgnoreCase) == true ||
        filament?.Contains("abs", StringComparison.OrdinalIgnoreCase) == true;
}
