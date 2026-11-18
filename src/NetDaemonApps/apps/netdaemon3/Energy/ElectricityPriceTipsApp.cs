using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using NetDaemon.Client;

namespace NetDaemonApps;

[NetDaemonApp]
public sealed class ElectricityPriceTipsApp : ReactiveApp, IAsyncInitializable
{
    private const string WindowSensor = "sensor.nasta_billiga_elperiod";
    private static readonly TimeZoneInfo MarketTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(15);

    private readonly IHaContext _haContext;
    private readonly Entities _entities;
    private readonly NordpoolServices _pricesService;
    private readonly IHomeAssistantRunner _homeAssistant;
    private readonly ITextToSpeechService _speech;
    private readonly IScheduler _scheduler;
    private readonly ILogger<ElectricityPriceTipsApp> _logger;
    private readonly ElectricityPriceTipsConfig _config;
    private readonly Dictionary<DateOnly, IReadOnlyList<ElectricityPriceInterval>> _prices = [];
    private readonly Dictionary<DateOnly, DateTimeOffset> _retryAfter = [];
    private string? _configEntry;
    private DateTimeOffset? _announcedUntil;
    private DateOnly? _announcedNight;
    private ElectricityPriceWindow? _publishedWindow;
    private IReadOnlyList<ElectricityPricePeriod> _publishedPeriods = [];
    private IReadOnlyList<ElectricityPriceInterval> _publishedPrices = [];
    private DateTimeOffset? _publishedPricesUntil;
    private bool _hasPublished;

    public ElectricityPriceTipsApp(
        IHaContext haContext,
        Entities entities,
        NordpoolServices pricesService,
        IHomeAssistantRunner homeAssistant,
        ITextToSpeechService speech,
        IScheduler scheduler,
        ILogger<ElectricityPriceTipsApp> logger,
        IAppConfig<ElectricityPriceTipsConfig> config)
    {
        _haContext = haContext;
        _entities = entities;
        _pricesService = pricesService;
        _homeAssistant = homeAssistant;
        _speech = speech;
        _scheduler = scheduler;
        _logger = logger;
        _config = config.Value;
        _config.Validate();
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        RestoreAnnouncements();
        var lifetime = Track(new CancellationDisposable());
        var nextMinute = WholeMinute(_scheduler.Now).AddMinutes(1);
        var ticks = Observable.Timer(nextMinute, TimeSpan.FromMinutes(1), _scheduler).Select(_ => Unit.Default);
        var triggers = Observable.Merge(
            ticks,
            _entities.Zone.Home.StateChanges().Select(_ => Unit.Default),
            _entities.InputSelect.HouseModeSelect.StateChanges().Select(_ => Unit.Default),
            _entities.BinarySensor.NordPoolSe2MorgondagensPrisTillgangligt.StateChanges()
                .Where(change => change.New?.State == HomeAssistantStates.On).Select(_ => Unit.Default));

        SubscribeLoggedAsync(triggers.StartWith(Unit.Default), _logger, "evaluate SE2 electricity price tips",
            _ => EvaluateAsync(lifetime.Token));
    }, cancellationToken);

    private void RestoreAnnouncements()
    {
        if (_haContext.GetState(WindowSensor)?.AttributesJson is not { ValueKind: JsonValueKind.Object } attributes)
        {
            return;
        }

        if (attributes.TryGetProperty("last_day_tip_end", out var day) &&
            day.ValueKind == JsonValueKind.String && day.TryGetDateTimeOffset(out var end))
        {
            _announcedUntil = end;
        }

        if (attributes.TryGetProperty("last_night_tip_date", out var night) &&
            night.ValueKind == JsonValueKind.String &&
            DateOnly.TryParseExact(night.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _announcedNight = date;
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        var now = WholeMinute(_scheduler.Now);
        var today = DateOnly.FromDateTime(ToLocal(now).DateTime);
        var tomorrow = today.AddDays(1);
        var midnight = MarketTime(tomorrow, TimeSpan.Zero);

        foreach (var expired in _prices.Keys.Where(date => date < today.AddDays(-1)).ToArray())
        {
            _prices.Remove(expired);
        }

        foreach (var expired in _retryAfter.Keys.Where(date => date < today).ToArray())
        {
            _retryAfter.Remove(expired);
        }

        await TryLoadDayAsync(today, cancellationToken).ConfigureAwait(false);
        if (_entities.BinarySensor.NordPoolSe2MorgondagensPrisTillgangligt.IsOn() ||
            ToLocal(now).TimeOfDay >= TimeSpan.FromHours(13))
        {
            await TryLoadDayAsync(tomorrow, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        now = WholeMinute(_scheduler.Now);
        if (now >= midnight)
        {
            return;
        }

        var days = _prices.OrderBy(day => day.Key).Select(day => day.Value).ToArray();
        var windows = ElectricityPricePolicy.FindCheapWindows(
            days,
            _config.MinimumWindow, _config.LowPriceSekPerKwh, _config.DayDiscountSekPerKwh);
        var next = windows.FirstOrDefault(window => window.End - now >= _config.MinimumWindow);
        ElectricityPriceWindow? nextWindow = next == default ? null : next;

        if (SomeoneHome() && HouseModeCodec.TryParse(_entities.InputSelect.HouseModeSelect.State, out var mode) &&
            mode != HouseMode.Night)
        {
            Announce(now, today, tomorrow, midnight, nextWindow);
        }

        var coverageEnd = _prices.ContainsKey(tomorrow) ? MarketTime(tomorrow.AddDays(1), TimeSpan.Zero) : midnight;
        DateTimeOffset? pricesUntil = _prices.ContainsKey(today) ? coverageEnd : null;
        var forecastEnd = now.AddHours(24);
        IReadOnlyList<ElectricityPricePeriod> periods = pricesUntil is null
            ? []
            : ElectricityPricePolicy.GetPeriodsInRange(windows, now, forecastEnd, _config.MinimumWindow);
        var prices = days.SelectMany(day => day)
            .Where(quarter => quarter.End > now && quarter.Start < forecastEnd)
            .ToArray();
        await PublishWindowAsync(nextWindow, periods, prices, pricesUntil, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryLoadDayAsync(DateOnly day, CancellationToken cancellationToken)
    {
        if (_prices.ContainsKey(day) || _retryAfter.GetValueOrDefault(day) > _scheduler.Now)
        {
            return;
        }

        try
        {
            _configEntry ??= await NordPoolPrices.GetConfigEntryAsync(
                GetConnection(), _entities.Sensor.NordPoolSe2AktuelltPris.EntityId, cancellationToken)
                .ConfigureAwait(false);
            _prices[day] = await NordPoolPrices.ReadDayAsync(
                _pricesService, _configEntry, day, MarketTime(day, TimeSpan.Zero),
                MarketTime(day.AddDays(1), TimeSpan.Zero), cancellationToken).ConfigureAwait(false);
            _retryAfter.Remove(day);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _retryAfter[day] = _scheduler.Now + RetryDelay;
            _logger.LogWarning(exception, "Could not load SE2 electricity prices for {Date}. Will retry.", day);
        }
    }

    private bool SomeoneHome() =>
        int.TryParse(_entities.Zone.Home.State, NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
        count > 0;

    private void Announce(
        DateTimeOffset now, DateOnly today, DateOnly tomorrow, DateTimeOffset midnight,
        ElectricityPriceWindow? nextWindow)
    {
        var messages = new List<string>(2);
        DateTimeOffset? dayTipEnd = null;
        var eveningStart = MarketTime(today, _config.EveningTipTime);
        ElectricityPriceWindow? nightTip = null;

        if (nextWindow is { } active && active.Start <= now &&
            (_announcedUntil is not { } announcedUntil || active.Start >= announcedUntil))
        {
            dayTipEnd = active.End;
            messages.Add($"Nu är det läge att köra disk eller tvätt. Elen är billig fram till {Clock(active.End, today)}.");
        }

        if (now >= eveningStart && _announcedNight != today &&
            _prices.TryGetValue(today, out var eveningPrices) &&
            _prices.TryGetValue(tomorrow, out var nightPrices))
        {
            nightTip = ElectricityPricePolicy.FindNightTip(eveningPrices, nightPrices, eveningStart,
                midnight, MarketTime(tomorrow, _config.NightWindowEnd), _config.MinimumWindow,
                _config.NightSavingsSekPerKwh);
            if (nightTip is { } night)
            {
                messages.Add($"Om ni vill köra en maskin i natt är elen billigast mellan klockan {ClockTime(night.Start)} och {ClockTime(night.End)}.");
            }
        }

        if (messages.Count == 0)
        {
            return;
        }

        try
        {
            _speech.Speak(_config.MediaPlayer, string.Join(' ', messages), _config.TextToSpeechEngine);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not announce electricity price tips");
            return;
        }
        _announcedUntil = dayTipEnd ?? _announcedUntil;
        _hasPublished = false;
        if (nightTip is not null)
        {
            _announcedNight = today;
        }
    }

    private async Task PublishWindowAsync(
        ElectricityPriceWindow? window,
        IReadOnlyList<ElectricityPricePeriod> periods,
        IReadOnlyList<ElectricityPriceInterval> prices,
        DateTimeOffset? pricesUntil,
        CancellationToken cancellationToken)
    {
        if (_hasPublished && window == _publishedWindow &&
            pricesUntil == _publishedPricesUntil &&
            periods.SequenceEqual(_publishedPeriods) && prices.SequenceEqual(_publishedPrices))
        {
            return;
        }

        var start = window is { } priceWindow ? ToLocal(priceWindow.Start).ToString("O") : null;
        var end = window is { } available ? ToLocal(available.End).ToString("O") : null;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        request.CancelAfter(TimeSpan.FromSeconds(20));
        var result = await GetConnection().PostApiCallAsync<JsonElement?>(
            $"states/{WindowSensor}",
            request.Token,
            new
            {
                state = start ?? "unavailable",
                attributes = new
                {
                    friendly_name = "Nästa billiga elperiod",
                    device_class = "timestamp",
                    start,
                    end,
                    average_price_sek_per_kwh = window?.AveragePrice,
                    periods_24h = periods.Select(period => new
                    {
                        start = ToLocal(period.Start),
                        end = ToLocal(period.End),
                    }).ToArray(),
                    prices_24h = prices.Select(quarter => new
                    {
                        start = ToLocal(quarter.Start),
                        end = ToLocal(quarter.End),
                        price_ore_per_kwh = quarter.Price * 100m,
                    }).ToArray(),
                    prices_until = pricesUntil is { } until ? ToLocal(until) : (DateTimeOffset?)null,
                    last_day_tip_end = _announcedUntil,
                    last_night_tip_date = _announcedNight,
                },
            }).ConfigureAwait(false);

        if (result is null)
        {
            throw new InvalidOperationException($"Home Assistant did not confirm the state of {WindowSensor}.");
        }

        _publishedWindow = window;
        _publishedPeriods = periods;
        _publishedPrices = prices;
        _publishedPricesUntil = pricesUntil;
        _hasPublished = true;
    }

    private IHomeAssistantConnection GetConnection() => _homeAssistant.CurrentConnection
        ?? throw new InvalidOperationException("No connection to Home Assistant.");

    private static DateTimeOffset WholeMinute(DateTimeOffset time) =>
        new(time.UtcTicks - time.UtcTicks % TimeSpan.TicksPerMinute, TimeSpan.Zero);

    private static DateTimeOffset MarketTime(DateOnly day, TimeSpan time)
    {
        var localTime = day.ToDateTime(TimeOnly.FromTimeSpan(time));
        return new DateTimeOffset(localTime, MarketTimeZone.GetUtcOffset(localTime));
    }

    private static DateTimeOffset ToLocal(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, MarketTimeZone);

    private static string ClockTime(DateTimeOffset time) => ToLocal(time).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Clock(DateTimeOffset time, DateOnly today) =>
        $"klockan {ClockTime(time)}" + (DateOnly.FromDateTime(ToLocal(time).DateTime) > today ? " i morgon" : "");
}
