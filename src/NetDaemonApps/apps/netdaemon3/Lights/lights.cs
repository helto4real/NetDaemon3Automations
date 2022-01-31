using System.Threading;
using System.Threading.Tasks;

public class LightsConfiguration
{
    public IEnumerable<BinarySensorEntity>? LivingRoomPirs { get; set; }
    public IEnumerable<BinarySensorEntity>? TvRoomPirs { get; set; }
    public BinarySensorEntity? KitchenPir { get; set; }
    public BinarySensorEntity? TomasRoomPir { get; set; }
    public RemoteEntity? RemoteTvRummet { get; set; }
    public LightEntity? ElgatoKeyLight { get; set; }
    public InputSelectEntity? HouseModeSelect { get; set; }
}

/// <summary>
///     Manage default lights and implements the following use-cases:
///     - Nightlights turn on in the night
///     - Activates the correct scene (RunScript) depending on time of day
/// </summary>
[NetDaemonApp]
public class LightManager
{
    private readonly LightsConfiguration _config;
    private readonly Entities _entities;

    private readonly Services _services;

    public LightManager(IHaContext ha, IAppConfig<LightsConfiguration> config)
    {
        _entities = new Entities(ha);
        _services = new Services(ha);
        _config = config.Value;
        Initialize();
    }

    /// <summary>
    ///     Returns true if it is currently night
    /// </summary>
    public bool IsNight => _config.HouseModeSelect?.State == "Natt";

    /// <summary>
    ///     Returns true if TV is currently on
    /// </summary>
    public bool IsTvOn => _config.RemoteTvRummet.IsOn();

    public void Initialize()
    {
        InitializeNightLights();

        InitializeTimeOfDayScenes();

        // handle keylights
        _config.TomasRoomPir?
            .StateChanges()
            .Where(e =>
                e.New.IsOff() &&
                e.Old.IsOn() &&
                _config.ElgatoKeyLight.IsOn()
            )
            .Throttle(TimeSpan.FromMinutes(30))
            .Subscribe(s => _config.ElgatoKeyLight?.TurnOff(0));

        _config.TomasRoomPir?
            .StateChanges()
            .Where(e =>
                e.New.IsOn() &&
                e.Old.IsOff() &&
                _config.ElgatoKeyLight.IsOff()
            )
            .Subscribe(s => _config.ElgatoKeyLight?.TurnOn(0));
    }

    /// <summary>
    ///     Initialize the scenes to call depending on time of day
    /// </summary>
    private void InitializeTimeOfDayScenes()
    {
        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Dag")
            .Subscribe(s => TurnOffAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Kväll")
            .Subscribe(s => TurnOnAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Natt")
            .Subscribe(s => TurnOffAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Morgon")
            .Subscribe(s => TurnOffAmbient());

        _config.HouseModeSelect?
            .StateChanges()
            .Where(e => e.New?.State == "Städning")
            .Subscribe(s => _services.Script.CleaningScene());
    }


    private void TurnOffAmbient()
    {
        _entities.Light.Vardagsrum.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.Kok.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.TomasRum.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.MelkersRum.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.SallysRum.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.Tvrummet.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.FarstukvistLed.TurnOff(0);
        Thread.Sleep(200);
        _entities.Light.Sovrum.TurnOff(0);
    }

    private void TurnOnAmbient()
    {
        _entities.Light.Vardagsrum.TurnOn(0, brightness: 150);
        Thread.Sleep(200);
        _entities.Light.Kok.TurnOn(0, brightness: 150);
        Thread.Sleep(200);
        _entities.Light.TomasRum.TurnOn(0, brightness: 150);
        Thread.Sleep(200);
        _entities.Light.MelkersRum.TurnOn(0, brightness: 150);
        Thread.Sleep(200);
        _entities.Light.SallysRum.TurnOn(0, brightness: 150);
        Thread.Sleep(200);
        _entities.Light.Tvrummet.TurnOn(0, brightness: 130);
        Thread.Sleep(200);
        _entities.Light.FarstukvistLed.TurnOn(0, brightness: 150);
        Thread.Sleep(200);
        _entities.Light.Sovrum.TurnOn(0, brightness: 20);
        Thread.Sleep(200);
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
                if (IsTimeNowBetween(TimeSpan.FromHours(5), TimeSpan.FromHours(10)))
                    _entities.Light.Vardagsrum.TurnOn(0);
                else
                    _entities.Light.HallByra.TurnOn(0);
            });

        // Turn off after som time idle except if it is morning then keep on until daytime will turn off
        _config.LivingRoomPirs?
            .StateChanges()
            .Where(e =>
                e.New.IsOff() &&
                e.Old.IsOn() &&
                IsNight &&
                !IsTimeNowBetween(TimeSpan.FromHours(5), TimeSpan.FromHours(10)))
            .Throttle(TimeSpan.FromMinutes(15))
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
                e.New.IsOff() &&
                e.Old.IsOn() &&
                IsNight &&
                !IsTimeNowBetween(TimeSpan.FromHours(5), TimeSpan.FromHours(10)))
            .Throttle(TimeSpan.FromMinutes(15))
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
                e.New.IsOff() &&
                e.Old.IsOn() &&
                IsNight &&
                !IsTvOn &&
                !IsTimeNowBetween(TimeSpan.FromHours(5), TimeSpan.FromHours(10)))
            .Throttle(TimeSpan.FromMinutes(15))
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
}
