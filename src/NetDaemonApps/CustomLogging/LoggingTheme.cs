using Serilog.Sinks.SystemConsole.Themes;
namespace NetDaemonApps;

public static class NetDaemonConsoleThemes
{
    public static AnsiConsoleTheme AnsiTheme { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\x1b[38;5;0253m",
        [ConsoleThemeStyle.SecondaryText] = "\x1b[38;5;0246m",
        [ConsoleThemeStyle.TertiaryText] = "\x1b[38;5;0242m",
        [ConsoleThemeStyle.Invalid] = "\x1b[33;1m",
        [ConsoleThemeStyle.Null] = "\x1b[38;5;0038m",
        [ConsoleThemeStyle.Number] = "\x1b[38;5;151m",
        [ConsoleThemeStyle.Boolean] = "\x1b[38;5;0038m",
        [ConsoleThemeStyle.LevelVerbose] = "\x1b[37m",
        [ConsoleThemeStyle.LevelError] = "\u001b[0;31m",
        [ConsoleThemeStyle.Name] = "\u001b[1;34m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[0;36m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[1;33m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[0;31m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[0;37m",
        [ConsoleThemeStyle.Scalar] = "\u001b[1;34m",
        [ConsoleThemeStyle.String] = "\u001b[0;36m"
    });
}
