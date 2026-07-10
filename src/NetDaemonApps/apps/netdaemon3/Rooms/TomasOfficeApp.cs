using System.Reactive.Concurrency;

namespace NetDaemonApps;

public sealed class TomasOfficeConfig
{
    public SwitchEntity JarvisMuteSwitch { get; init; } = null!;

    public string BlindsMqttDevice { get; init; } = "tomas_rum_knapp_rullgardin";

    public TimeSpan KeyLightAbsenceTimeout { get; init; } = TimeSpan.FromMinutes(20);

    public TimeSpan WardrobeDoorDebounce { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan WardrobeOpenTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan WardrobeLightTimeout { get; init; } = TimeSpan.FromMinutes(30);
}

[NetDaemonApp]
public sealed class TomasOfficeApp : ReactiveApp, IAsyncInitializable
{
    private readonly Entities _entities;
    private readonly IScheduler _scheduler;
    private readonly ITriggerManager _triggerManager;
    private readonly ILogger<TomasOfficeApp> _logger;
    private readonly TomasOfficeConfig _config;

    public TomasOfficeApp(
        Entities entities,
        ITriggerManager triggerManager,
        IScheduler scheduler,
        ILogger<TomasOfficeApp> logger,
        IAppConfig<TomasOfficeConfig> config)
    {
        _entities = entities;
        _scheduler = scheduler;
        _triggerManager = triggerManager;
        _logger = logger;
        _config = config.Value;

        ArgumentNullException.ThrowIfNull(_config.JarvisMuteSwitch);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.JarvisMuteSwitch.EntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.BlindsMqttDevice);
        ConfigurationValidation.Positive(
            _config.KeyLightAbsenceTimeout,
            nameof(TomasOfficeConfig.KeyLightAbsenceTimeout));
        ConfigurationValidation.NonNegative(
            _config.WardrobeDoorDebounce,
            nameof(TomasOfficeConfig.WardrobeDoorDebounce));
        ConfigurationValidation.Positive(
            _config.WardrobeOpenTimeout,
            nameof(TomasOfficeConfig.WardrobeOpenTimeout));
        ConfigurationValidation.Positive(
            _config.WardrobeLightTimeout,
            nameof(TomasOfficeConfig.WardrobeLightTimeout));
    }

    private bool IsNight => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Night;

    private bool IsEvening => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Evening;

    private bool SomeoneIsInRoom =>
        _entities.BinarySensor.TomasRumEprlZone1Occupancy.IsOn() ||
        _entities.BinarySensor.TomasRumEprlZone2Occupancy.IsOn();

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        InitializeJarvisSchedule();
        InitializeBlinds();
        InitializeDeskLights();
        InitializeKeyLight();
        InitializeAmbientLighting();
        InitializeWardrobeLight();

    }, cancellationToken);

    private void InitializeJarvisSchedule()
    {
        Track(_scheduler.ScheduleCron("0 8 * * 1-5", () => _config.JarvisMuteSwitch.TurnOn()));
        Track(_scheduler.ScheduleCron("30 16 * * 1-5", () => _config.JarvisMuteSwitch.TurnOff()));
    }

    private void InitializeBlinds()
    {
        SubscribeLogged(
            _triggerManager.RegisterMqttActionTrigger(_config.BlindsMqttDevice).Where(action => action is not null),
            _logger,
            "control Tomas office blinds",
            action =>
            {
                switch (action)
                {
                    case HomeAssistantStates.Open:
                        _entities.Cover.TomasRumRullgardin.OpenCover();
                        break;
                    case HomeAssistantStates.Close:
                        _entities.Cover.TomasRumRullgardin.CloseCover();
                        break;
                }
            });
    }

    private void InitializeDeskLights()
    {
        SubscribeLogged(
            PresenceChanges().Where(state => state.Zone1On || state.Zone2On),
            _logger,
            "update office desk light",
            state =>
            {
                var color = state.Zone2On ? DeskSceneColors.AtDesk : DeskSceneColors.NotAtDesk;
                _entities.Light.TomasRumLedSkrivbord.TurnOn(rgbColor: color);
            });

        SubscribeLogged(
            PresenceChanges().Where(state => !state.Zone1On && !state.Zone2On),
            _logger,
            "turn off office desk light",
            _ => _entities.Light.TomasRumLedSkrivbord.TurnOff());
    }

    private void InitializeKeyLight()
    {
        SubscribeLogged(
            _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
                .Where(change => change.New.IsOn() && _entities.Light.ElgatoKeyLight.IsOff()),
            _logger,
            "turn on office key light",
            _ => _entities.Light.ElgatoKeyLight.TurnOn(brightnessPct: 6, colorTempKelvin: 4800));

        SubscribeLogged(
            _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
                .WhenStateIsFor(
                    state => state.IsOff() && _entities.Light.ElgatoKeyLight.IsOn(),
                    _config.KeyLightAbsenceTimeout,
                    _scheduler),
            _logger,
            "turn off office key light",
            _ => _entities.Light.ElgatoKeyLight.TurnOff());
    }

    private void InitializeAmbientLighting()
    {
        SubscribeLogged(
            _entities.Cover.TomasRumRullgardin.StateChanges()
                .Where(change => change.New?.State == HomeAssistantStates.Closed && SomeoneIsInRoom),
            _logger,
            "turn on office ambient light from blinds",
            _ => TurnOnAmbientLight());

        SubscribeLogged(
            _entities.Cover.TomasRumRullgardin.StateChanges()
                .Where(change => change.New?.State == HomeAssistantStates.Open && !IsNight && !IsEvening),
            _logger,
            "turn off office ambient light from blinds",
            _ => TurnOffAmbientLight());

        SubscribeLogged(
            PresenceChanges().Where(state =>
                (IsNight || !IsNight && !IsEvening && _entities.Cover.TomasRumRullgardin.State == HomeAssistantStates.Closed) &&
                (state.Zone1On || state.Zone2On)),
            _logger,
            "turn on office ambient light from presence",
            _ => TurnOnAmbientLight());

        SubscribeLogged(
            PresenceChanges().Where(state =>
                (IsNight || !IsNight && !IsEvening && _entities.Cover.TomasRumRullgardin.State == HomeAssistantStates.Closed) &&
                !state.Zone1On && !state.Zone2On),
            _logger,
            "turn off office ambient light from presence",
            _ => TurnOffAmbientLight());
    }

    private void InitializeWardrobeLight()
    {
        SubscribeLogged(
            _entities.BinarySensor.TomasRumDoorSensorWardrobe.StateChanges()
                .WhenStateIsFor(state => state.IsOn(), _config.WardrobeDoorDebounce, _scheduler),
            _logger,
            "turn on wardrobe light",
            _ => _entities.Light.TomasRumLedlampaGarderob.TurnOn());

        SubscribeLogged(
            _entities.BinarySensor.TomasRumDoorSensorWardrobe.StateChanges()
                .WhenStateIsFor(state => state.IsOn(), _config.WardrobeOpenTimeout, _scheduler),
            _logger,
            "turn off wardrobe light after open timeout",
            _ => _entities.Light.TomasRumLedlampaGarderob.TurnOff());

        SubscribeLogged(
            _entities.Light.TomasRumLedlampaGarderob.StateChanges()
                .WhenStateIsFor(state => state.IsOn(), _config.WardrobeLightTimeout, _scheduler),
            _logger,
            "turn off wardrobe light after safety timeout",
            _ => _entities.Light.TomasRumLedlampaGarderob.TurnOff());

        SubscribeLogged(
            _entities.BinarySensor.TomasRumDoorSensorWardrobe.StateChanges()
                .WhenStateIsFor(state => state.IsOff(), _config.WardrobeDoorDebounce, _scheduler),
            _logger,
            "turn off wardrobe light when door closes",
            _ => _entities.Light.TomasRumLedlampaGarderob.TurnOff());
    }

    private IObservable<(bool Zone1On, bool Zone2On)> PresenceChanges() =>
        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(
                _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(),
                (zone1, zone2) => (zone1.New.IsOn(), zone2.New.IsOn()));

    private void TurnOnAmbientLight()
    {
        _entities.Light.TomasRum.TurnOn();
        _entities.Light.TomasRumLedBokhylla.TurnOn(
            rgbColor: DeskSceneColors.NotAtDesk,
            brightnessPct: 59,
            transition: 0);
    }

    private void TurnOffAmbientLight()
    {
        _entities.Light.TomasRum.TurnOff();
        _entities.Light.TomasRumLedBokhylla.TurnOff();
    }
}

public static class DeskSceneColors
{
    public static IReadOnlyList<int> AtDesk { get; } = [255, 228, 205];

    public static IReadOnlyList<int> NotAtDesk { get; } = [0, 0, 255];
}
