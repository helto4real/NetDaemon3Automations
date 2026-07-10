using System.Reactive.Concurrency;

namespace NetDaemonApps;

public sealed class LightsConfiguration
{
    public IReadOnlyCollection<BinarySensorEntity> LivingRoomPirs { get; init; } = null!;

    public IReadOnlyCollection<BinarySensorEntity> TvRoomPirs { get; init; } = null!;

    public BinarySensorEntity KitchenPir { get; init; } = null!;

    public RemoteEntity RemoteTvRummet { get; init; } = null!;

    public InputSelectEntity HouseModeSelect { get; init; } = null!;

    public TimeSpan CommandDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan NightLightTimeout { get; init; } = TimeSpan.FromMinutes(15);

    public int AmbientBrightnessPercent { get; init; } = 59;

    public int TvRoomBrightnessPercent { get; init; } = 51;

    public int BedroomBrightnessPercent { get; init; } = 8;

    public TimeSpan MorningStart { get; init; } = new(5, 0, 0);

    public TimeSpan MorningEnd { get; init; } = new(10, 0, 0);

    public TimeSpan KitchenMorningEnd { get; init; } = new(9, 0, 0);
}

[NetDaemonApp]
public sealed class LightManager : ReactiveApp, IAsyncInitializable
{
    private readonly LightsConfiguration _config;
    private readonly IScheduler _scheduler;
    private readonly Entities _entities;
    private readonly ILogger<LightManager> _logger;
    private readonly ICommandSequencer _commandSequencer;
    private readonly TimeProvider _timeProvider;
    private CancellationToken _cancellationToken;

    public LightManager(
        IHaContext haContext,
        IAppConfig<LightsConfiguration> config,
        IScheduler scheduler,
        ILogger<LightManager> logger,
        ICommandSequencer commandSequencer,
        TimeProvider timeProvider)
    {
        _entities = new Entities(haContext);
        _config = config.Value;
        _scheduler = scheduler;
        _logger = logger;
        _commandSequencer = commandSequencer;
        _timeProvider = timeProvider;

        ValidateConfiguration(_config);
    }

    private bool IsNight => _config.HouseModeSelect.State == HouseModeCodec.Night;

    private bool IsTvOn => _config.RemoteTvRummet.IsOn();

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        _cancellationToken = cancellationToken;

        InitializeNightLights();
        InitializeTimeOfDayScenes();
    }, cancellationToken);

    private void InitializeTimeOfDayScenes()
    {
        SubscribeLoggedAsync(
            _config.HouseModeSelect.StateChanges().Where(change => change.New?.State == HouseModeCodec.Day),
            _logger,
            "turn off ambient lights for day mode",
            _ => TurnOffAmbientAsync());

        SubscribeLoggedAsync(
            _config.HouseModeSelect.StateChanges().Where(change => change.New?.State == HouseModeCodec.Evening),
            _logger,
            "turn on ambient lights for evening mode",
            _ => TurnOnAmbientAsync());

        SubscribeLoggedAsync(
            _config.HouseModeSelect.StateChanges()
                .Where(change => change.New?.State is HouseModeCodec.Night or HouseModeCodec.Morning),
            _logger,
            "turn off ambient lights for night or morning mode",
            _ => TurnOffAmbientAsync());
    }

    private async Task TurnOffAmbientAsync()
    {
        var actions = new Action[]
        {
            () => _entities.Light.Vardagsrum.TurnOff(0),
            () => _entities.Light.Kok.TurnOff(0),
            () => _entities.Light.TomasRum.TurnOff(0),
            () => _entities.Light.ElinsRum.TurnOff(0),
            () => _entities.Light.SallysRum.TurnOff(0),
            () => _entities.Light.Tvrummet.TurnOff(0),
            () => _entities.Light.FarstukvistLed.TurnOff(0),
            () => _entities.Light.Sovrum.TurnOff(0),
        };

        await ExecuteActionsAsync(actions, "turn off ambient lights").ConfigureAwait(false);
    }

    private async Task TurnOnAmbientAsync()
    {
        var actions = new Action[]
        {
            () => _entities.Light.Vardagsrum.TurnOn(0, brightnessPct: _config.AmbientBrightnessPercent),
            () => _entities.Light.Kok.TurnOn(0, brightnessPct: _config.AmbientBrightnessPercent),
            () => _entities.Light.TomasRum.TurnOn(0, brightnessPct: _config.AmbientBrightnessPercent),
            () => _entities.Light.ElinsRum.TurnOn(0, brightnessPct: _config.AmbientBrightnessPercent),
            () => _entities.Light.SallysRum.TurnOn(0, brightnessPct: _config.AmbientBrightnessPercent),
            () => _entities.Light.Tvrummet.TurnOn(0, brightnessPct: _config.TvRoomBrightnessPercent),
            () => _entities.Light.FarstukvistLed.TurnOn(0, brightnessPct: _config.AmbientBrightnessPercent),
            () => _entities.Light.Sovrum.TurnOn(0, brightnessPct: _config.BedroomBrightnessPercent),
        };

        await ExecuteActionsAsync(actions, "turn on ambient lights").ConfigureAwait(false);
    }

    private Task ExecuteActionsAsync(IEnumerable<Action> actions, string operation) => ExecuteLoggedAsync(
        _commandSequencer,
        actions,
        _config.CommandDelay,
        _logger,
        operation,
        _cancellationToken);

    private void InitializeNightLights()
    {
        SubscribeLogged(
            _config.LivingRoomPirs.StateChanges().Where(change => change.New.IsOn() && IsNight),
            _logger,
            "turn on living-room night light",
            _ =>
            {
                if (IsMorningWindow(_config.MorningEnd))
                {
                    _entities.Light.Vardagsrum.TurnOn(0);
                }
                else
                {
                    _entities.Light.HallByra.TurnOn(0);
                }
            });

        SubscribeLogged(
            _config.LivingRoomPirs.StateChanges()
                .Where(_ => IsNight && !IsMorningWindow(_config.MorningEnd))
                .WhenStateIsFor(state => state.IsOff(), _config.NightLightTimeout, _scheduler),
            _logger,
            "turn off living-room night light",
            _ => _entities.Light.HallByra.TurnOff(0));

        SubscribeLogged(
            _config.KitchenPir.StateChanges()
                .Where(change => change.New.IsOn() && change.Old.IsOff() && IsNight),
            _logger,
            "turn on kitchen night light",
            _ => _entities.Light.Kok.TurnOn(0));

        SubscribeLogged(
            _config.KitchenPir.StateChanges()
                .Where(_ => IsNight && !IsMorningWindow(_config.KitchenMorningEnd))
                .WhenStateIsFor(state => state.IsOff(), _config.NightLightTimeout, _scheduler),
            _logger,
            "turn off kitchen night light",
            _ => _entities.Light.Kok.TurnOff(0));

        SubscribeLogged(
            _config.TvRoomPirs.StateChanges()
                .Where(_ => IsNight && !IsTvOn && !IsMorningWindow(_config.KitchenMorningEnd))
                .WhenStateIsFor(state => state.IsOff(), _config.NightLightTimeout, _scheduler),
            _logger,
            "turn off TV-room night light",
            _ => _entities.Light.Tvrummet.TurnOff(0));
    }

    private bool IsMorningWindow(TimeSpan end)
    {
        var now = TimeOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        return TimeWindowPolicy.IsTimeBetween(
            now,
            TimeOnly.FromTimeSpan(_config.MorningStart),
            TimeOnly.FromTimeSpan(end));
    }

    private static void ValidateConfiguration(LightsConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration.LivingRoomPirs);
        ArgumentNullException.ThrowIfNull(configuration.TvRoomPirs);
        ArgumentNullException.ThrowIfNull(configuration.KitchenPir);
        ArgumentNullException.ThrowIfNull(configuration.RemoteTvRummet);
        ArgumentNullException.ThrowIfNull(configuration.HouseModeSelect);

        ConfigurationValidation.NonNegative(configuration.CommandDelay, nameof(LightsConfiguration.CommandDelay));
        ConfigurationValidation.Positive(
            configuration.NightLightTimeout,
            nameof(LightsConfiguration.NightLightTimeout));
        ConfigurationValidation.DailyTime(configuration.MorningStart, nameof(LightsConfiguration.MorningStart));
        ConfigurationValidation.DailyTime(configuration.MorningEnd, nameof(LightsConfiguration.MorningEnd));
        ConfigurationValidation.DailyTime(
            configuration.KitchenMorningEnd,
            nameof(LightsConfiguration.KitchenMorningEnd));

        ArgumentOutOfRangeException.ThrowIfNegative(configuration.AmbientBrightnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(configuration.AmbientBrightnessPercent, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(configuration.TvRoomBrightnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(configuration.TvRoomBrightnessPercent, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(configuration.BedroomBrightnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(configuration.BedroomBrightnessPercent, 100);

        if (configuration.LivingRoomPirs.Count == 0 || configuration.TvRoomPirs.Count == 0)
        {
            throw new InvalidOperationException("Light sensor collections must not be empty.");
        }

        if (configuration.MorningStart >= configuration.MorningEnd ||
            configuration.MorningStart >= configuration.KitchenMorningEnd)
        {
            throw new InvalidOperationException("Morning light windows must end after they start.");
        }
    }
}
