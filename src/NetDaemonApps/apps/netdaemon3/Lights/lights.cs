using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Configuration class for lights settings.
/// </summary>
public class LightsConfiguration
{
    /// <summary>
    /// Gets or sets the living room PIR sensors.
    /// </summary>
    public IEnumerable<BinarySensorEntity>? LivingRoomPirs { get; set; }
    /// <summary>
    /// Gets or sets the TV room PIR sensors.
    /// </summary>
    public IEnumerable<BinarySensorEntity>? TvRoomPirs { get; set; }
    /// <summary>
    /// Gets or sets the kitchen PIR sensor.
    /// </summary>
    public BinarySensorEntity? KitchenPir { get; set; }
    /// <summary>
    /// Gets or sets the Tomas room PIR sensor.
    /// </summary>
    public BinarySensorEntity? TomasRoomPir { get; set; }
    /// <summary>
    /// Gets or sets the remote for the TV room.
    /// </summary>
    public RemoteEntity? RemoteTvRummet { get; set; }
    /// <summary>
    /// Gets or sets the Elgato Key Light.
    /// </summary>
    public LightEntity? ElgatoKeyLight { get; set; }
    /// <summary>
    /// Gets or sets the house mode select entity.
    /// </summary>
    public InputSelectEntity? HouseModeSelect { get; set; }

    /// <summary>
    /// Gets or sets the light delay in milliseconds.
    /// </summary>
    public int LightDelayMs { get; set; } = 200;
    /// <summary>
    /// Gets or sets the night light timeout in minutes.
    /// </summary>
    public int NightLightTimeoutMinutes { get; set; } = 15;
    /// <summary>
    /// Gets or sets the ambient brightness.
    /// </summary>
    public int AmbientBrightness { get; set; } = 150;
    /// <summary>
    /// Gets or sets the TV room brightness.
    /// </summary>
    public int TvRoomBrightness { get; set; } = 130;
    /// <summary>
    /// Gets or sets the bedroom brightness.
    /// </summary>
    public int BedroomBrightness { get; set; } = 20;
    /// <summary>
    /// Gets or sets the morning start hour.
    /// </summary>
    public int MorningStartHour { get; set; } = 5;
    /// <summary>
    /// Gets or sets the morning end hour.
    /// </summary>
    public int MorningEndHour { get; set; } = 10;
    /// <summary>
    /// Gets or sets the kitchen morning end hour.
    /// </summary>
    public int KitchenMorningEndHour { get; set; } = 9;
}

/// <summary>
///     Manage default lights and implements the following use-cases:
///     - Nightlights turn on in the night
///     - Activates the correct scene (RunScript) depending on time of day
/// </summary>
[NetDaemonApp]
public class LightManager : IAsyncInitializable
{
    private readonly LightsConfiguration _config;
    private readonly IScheduler _scheduler;
    private readonly Entities _entities;
    private readonly ILogger<LightManager> _logger;

    private readonly Services _services;
    private CancellationToken? _cancellationToken { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LightManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="config">The configuration for lights.</param>
    /// <param name="scheduler">The scheduler for timing operations.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public LightManager(IHaContext ha, IAppConfig<LightsConfiguration> config, IScheduler scheduler, ILogger<LightManager> logger)
    {
        _entities = new Entities(ha);
        _services = new Services(ha);
        _config = config.Value;
        _scheduler = scheduler;
        _logger = logger;
        Initialize();
    }

    /// <summary>
    ///     Returns true if it is currently night
    /// </summary>
    public bool IsNight => _config.HouseModeSelect?.State == "Natt";

    /// <summary>
    ///     Returns true if TV is currently on
    /// </summary>
    public bool IsTvOn => _config.RemoteTvRummet?.IsOn() ?? false;

    /// <summary>
    /// Initializes the light manager.
    /// </summary>
    public void Initialize()
    {
        InitializeNightLights();

        InitializeTimeOfDayScenes();
        // handle keylights
    }

    /// <summary>
    ///     Initialize the scenes to call depending on time of day
    /// </summary>
    private void InitializeTimeOfDayScenes()
    {
        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Dag")
            .SubscribeAsync(s => TurnOffAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Kväll")
            .SubscribeAsync(s => TurnOnAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Natt")
            .SubscribeAsync(s => TurnOffAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Morgon")
            .SubscribeAsync(s => TurnOffAmbient());

    }


    private async Task TurnOffAmbient()
    {
    if (_cancellationToken?.IsCancellationRequested ?? true)
        return;
    try
    {
        var actions = new List<Action>
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
        await ExecuteWithDelays(actions);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error turning off ambient lights");
    }
}

    private async Task TurnOnAmbient()
    {
    if (_cancellationToken?.IsCancellationRequested ?? true)
        return;
    try
    {
        var actions = new List<Action>
        {
            () => _entities.Light.Vardagsrum.TurnOn(0, brightness: _config.AmbientBrightness),
            () => _entities.Light.Kok.TurnOn(0, brightness: _config.AmbientBrightness),
            () => _entities.Light.TomasRum.TurnOn(0, brightness: _config.AmbientBrightness),
            () => _entities.Light.ElinsRum.TurnOn(0, brightness: _config.AmbientBrightness),
            () => _entities.Light.SallysRum.TurnOn(0, brightness: _config.AmbientBrightness),
            () => _entities.Light.Tvrummet.TurnOn(0, brightness: _config.TvRoomBrightness),
            () => _entities.Light.FarstukvistLed.TurnOn(0, brightness: _config.AmbientBrightness),
            () => _entities.Light.Sovrum.TurnOn(0, brightness: _config.BedroomBrightness),
        };
        await ExecuteWithDelays(actions);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error turning on ambient lights");
    }
    }

    private async Task ExecuteWithDelays(IEnumerable<Action> actions)
    {
        foreach (var action in actions)
        {
            action();
        await Task.Delay(_config.LightDelayMs, _cancellationToken!.Value);
        }
    }

    /// <summary>
    ///     Setup the night lights.
    /// </summary>
    private void InitializeNightLights()
    {
        // Living room night lights, turns on when motion
        _config.LivingRoomPirs?
            .StateChanges()
            .Where(e => e.New.IsOn())
            .Where(e => IsNight)
            .Subscribe(s =>
            {
                // If morning time then turn on more lights
                if (IsTimeNowBetween(TimeSpan.FromHours(_config.MorningStartHour), TimeSpan.FromHours(_config.MorningEndHour)))
                    _entities.Light.Vardagsrum.TurnOn(0);
                else
                    _entities.Light.HallByra.TurnOn(0);
            });

        // Turn off after som time idle except if it is morning then keep on until daytime will turn off
        _config.LivingRoomPirs?
            .StateChanges()
            .Where(e =>
                IsNight &&
                !IsTimeNowBetween(TimeSpan.FromHours(_config.MorningStartHour), TimeSpan.FromHours(_config.MorningEndHour)))
            .WhenStateIsFor(e=>e.IsOff(), TimeSpan.FromMinutes(_config.NightLightTimeoutMinutes), _scheduler)
            .Subscribe(s => _entities.Light.HallByra.TurnOff(0));

        // Kitchen night lights
        _config.KitchenPir?
            .StateChanges()
            .Where(e =>
                e.New.IsOn() &&
                e.Old.IsOff() &&
                IsNight)
            .Subscribe(s => _entities.Light.Kok.TurnOn(0));

        _config.KitchenPir?
            .StateChanges()
            .Where(e =>
                IsNight &&
                !IsTimeNowBetween(TimeSpan.FromHours(_config.MorningStartHour), TimeSpan.FromHours(_config.KitchenMorningEndHour)))
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(_config.NightLightTimeoutMinutes), _scheduler)
            .Subscribe(s => _entities.Light.Kok.TurnOff(0));

        // TV Room night lights, only at night and not TV is on
        _config.TvRoomPirs?
            .StateChanges()
            .Where(e =>
                e.New.IsOn() &&
                e.Old.IsOff() &&
                IsNight &&
                !IsTvOn
            )
            .Subscribe(s => _entities.Light.Tvrummet.TurnOn(0));

        _config.TvRoomPirs?
            .StateChanges()
            .Where(e =>
                IsNight &&
                !IsTvOn &&
                !IsTimeNowBetween(TimeSpan.FromHours(_config.MorningStartHour), TimeSpan.FromHours(_config.KitchenMorningEndHour)))
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromMinutes(_config.NightLightTimeoutMinutes), _scheduler)
            .Subscribe(s => _entities.Light.Tvrummet.TurnOff(0)); //Entity("light.tvrummet")
    }

    // Todo, make this part of Fluent API
    private static bool IsTimeNowBetween(TimeSpan fromSpan, TimeSpan toSpan)
    {
        var now = DateTime.Now.TimeOfDay;
        if (now >= fromSpan && now <= toSpan)
            return true;

        return false;
    }

    /// <summary>
    /// Initializes the light manager asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}
