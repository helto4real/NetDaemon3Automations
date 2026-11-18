namespace NetDaemonApps;

public sealed class ElectricityPriceTipsConfig
{
    public string MediaPlayer { get; init; } = "media_player.huset";
    public string TextToSpeechEngine { get; init; } = "cloud_say";
    public TimeSpan MinimumWindow { get; init; } = TimeSpan.FromHours(2);
    public decimal LowPriceSekPerKwh { get; init; } = 0.30m;
    public decimal DayDiscountSekPerKwh { get; init; } = 0.20m;
    public decimal NightSavingsSekPerKwh { get; init; } = 0.30m;
    public TimeSpan EveningTipTime { get; init; } = TimeSpan.FromHours(19);
    public TimeSpan NightWindowEnd { get; init; } = TimeSpan.FromHours(6);

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(MediaPlayer);
        ArgumentException.ThrowIfNullOrWhiteSpace(TextToSpeechEngine);
        ConfigurationValidation.DailyTime(EveningTipTime, nameof(EveningTipTime));
        ConfigurationValidation.DailyTime(NightWindowEnd, nameof(NightWindowEnd));

        if (MinimumWindow <= TimeSpan.Zero ||
            MinimumWindow.Ticks % ElectricityPricePolicy.Quarter.Ticks != 0 ||
            MinimumWindow > NightWindowEnd ||
            MinimumWindow > TimeSpan.FromDays(1) - EveningTipTime)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumWindow), MinimumWindow,
                "Use full quarters and a duration that fits both the evening and night.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(DayDiscountSekPerKwh);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(NightSavingsSekPerKwh);
    }
}
