using System.Reactive.Concurrency;
using System.Text.Json;

/// <summary>
/// Automation app for Tomas' office managing all automations related to this room
/// including lighting, blinds, climate control and device management
/// </summary>
[NetDaemonApp]
// [Focus]
public class TomasOfficeApp
{
    private readonly Entities _entities;
    private readonly IScheduler _scheduler;
    private readonly ITextToSpeechService _tts;
    private readonly Services _services;
    private readonly ITriggerManager _triggerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TomasOfficeApp"/> class.
    /// Sets up all the automations for Tomas' office room
    /// </summary>
    /// <param name="entities">Entity manager for accessing Home Assistant entities</param>
    /// <param name="services">Service manager for calling Home Assistant services</param>
    /// <param name="triggerManager">Trigger manager for registering external triggers</param>
    /// <param name="tts">Text-to-speech service</param>
    /// <param name="scheduler">Scheduler for timing operations</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public TomasOfficeApp(Entities entities, Services services, ITriggerManager triggerManager, ITextToSpeechService tts, IScheduler scheduler, ILogger<TomasOfficeApp> logger)
    {
        _entities = entities;
        _scheduler = scheduler;
        _tts = tts;
        _services = services;
        _triggerManager = triggerManager;

        HandleBlinds();
        HandleDeskRgbLightsOnPresence();
        HandleKeyLightWhenSittingAtDesk();
        HandleAmbientDependingOnBlindState();
        HandleAmbientWhenPeopleAreInTheRoomDuringNighttime();
        HandleAmbientWhenPeopleAreInTheRoomDuringDayAndMorningWhenBlindsAreDown();
        HandleWardrobeLight();
        HandleJarvisAvailability();
    }

    /// <summary>
    /// Manages Jarvis availability - turns off during office hours
    /// Jarvis is muted at 8:00 AM and unmuted at 4:30 PM on weekdays
    /// </summary>
    private void HandleJarvisAvailability()
    {
        _scheduler.ScheduleCron("0 8 * * 1-5", () => _entities.Switch.JarvisTysta.TurnOn());
        _scheduler.ScheduleCron("30 16 * * 1-5", () => _entities.Switch.JarvisTysta.TurnOff());
    }

    /// <summary>
    /// Controls the office blinds based on MQTT button triggers
    /// Responds to "open" and "close" commands from the physical button
    /// </summary>
    private void HandleBlinds()
    {
        _triggerManager.RegisterMqttActionTrigger("tomas_rum_knapp_rullgardin")
            .Where(n => n is not null)
            .Subscribe(s =>
            {
                switch (s)
                {
                    case "open":
                        _entities.Cover.TomasRumRullgardin.OpenCover();
                        break;
                    case "close":
                        _entities.Cover.TomasRumRullgardin.CloseCover();
                        break;
                }
            });
    }

    /// <summary>
    /// Property indicating if the current house mode is Nighttime
    /// </summary>
    private bool IsNighttime => _entities.InputSelect.HouseModeSelect.State == "Natt";
    
    /// <summary>
    /// Property indicating if the current house mode is Evening
    /// </summary>
    private bool IsEveningtime => _entities.InputSelect.HouseModeSelect.State == "Kväll";
    
    /// <summary>
    /// Property indicating if any of the presence sensors detect someone in the room
    /// </summary>
    private bool SomeoneIsInRoom => _entities.BinarySensor.TomasRumEprlZone2Occupancy.State == "on" || 
                                    _entities.BinarySensor.TomasRumEprlZone1Occupancy.State == "on";

    /// <summary>
    /// Controls ambient lighting based on blind states
    /// - Turns on ambient light when blinds are closed and someone is in the room
    /// - Turns off ambient light when blinds are opened during daytime
    /// </summary>
    private void HandleAmbientDependingOnBlindState()
    {
        _entities.Cover.TomasRumRullgardin.StateChanges()
            .Where(n => n.New?.State == "closed" && SomeoneIsInRoom)
            .Subscribe(_ => TurnOnAmbientLight());

        _entities.Cover.TomasRumRullgardin.StateChanges()
           .Where(n => n.New?.State == "open" && !IsNighttime && !IsEveningtime)
           .Subscribe(_ => TurnOffAmbientLight());
    }

    /// <summary>
    /// Controls RGB desk lighting based on presence detection
    /// - Changes color based on whether person is at desk or just in the room
    /// - Turns off when no one is detected in the room
    /// </summary>
    private void HandleDeskRgbLightsOnPresence()
    {
        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n => n.zone1.New?.State == "on" || n.zone2.New?.State == "on")
            .Subscribe(s =>
            {
                var rgb = (s.zone1.New?.State, s.zone2.New?.State) switch
                {
                    (_, "on") => DeskSceneColors.AtDesk,     // Someone is at desk
                    (_, "off") => DeskSceneColors.NotAtDesk, // Someone in room, not at desk
                    _ => DeskSceneColors.NotInRoom           // Default fallback
                };
                
                if (rgb != DeskSceneColors.NotInRoom)
                {
                    _entities.Light.TomasRumLedSkrivbord.TurnOn(rgbColor: rgb);
                }
            });

        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n => n.zone2.New?.State == "off" && n.zone1.New?.State == "off")
            .Subscribe(_ => _entities.Light.TomasRumLedSkrivbord.TurnOff());
    }

    /// <summary>
    /// Controls the Elgato Key Light based on desk presence
    /// - Turns on immediately when person sits at desk
    /// - Turns off after 20 minutes of absence from desk
    /// </summary>
    private void HandleKeyLightWhenSittingAtDesk()
    {
        _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
            .Where(n => n.New?.State == "on" && _entities.Light.ElgatoKeyLight.State == "off")
            .Subscribe(_ => _entities.Light.ElgatoKeyLight.TurnOn(brightness: 15, colorTempKelvin: 4800));

        _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
            .WhenStateIsFor(n => n?.State == "off" && _entities.Light.ElgatoKeyLight.State == "on", 
                           TimeSpan.FromMinutes(20), _scheduler)
            .Subscribe(_ => _entities.Light.ElgatoKeyLight.TurnOff());
    }

/// <summary>
/// Controls ambient lighting during nighttime based on room presence
/// - Turns on ambient lighting when someone enters the room at night
/// - Turns off ambient lighting when room is empty at night
/// </summary>
private void HandleAmbientWhenPeopleAreInTheRoomDuringNighttime()
    {
        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n =>
                    IsNighttime &&
                    (n.zone1.New?.State == "on" || n.zone2.New?.State == "on")
                    )
            .Subscribe(_ => TurnOnAmbientLight());

        _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n =>
                    IsNighttime &&
                    n.zone2.New?.State == "off" && n.zone1.New?.State == "off")
            .Subscribe(_ => TurnOffAmbientLight());
    }

/// <summary>
/// Controls ambient lighting during daytime when blinds are closed
/// - Turns on ambient light when someone enters during day/morning with blinds closed
/// - Turns off ambient light when room is empty during day/morning with blinds closed
/// </summary>
private void HandleAmbientWhenPeopleAreInTheRoomDuringDayAndMorningWhenBlindsAreDown()
    {
        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n =>
                    !IsNighttime && !IsEveningtime &&
                    _entities.Cover.TomasRumRullgardin.State == "closed" &&
                    (n.zone1.New?.State == "on" || n.zone2.New?.State == "on")
                    )
            .Subscribe(_ => TurnOnAmbientLight());


        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n =>
                    !IsNighttime && !IsEveningtime &&
                    _entities.Cover.TomasRumRullgardin.State == "closed" &&
                    n.zone2.New?.State == "off" && n.zone1.New?.State == "off")
            .Subscribe(_ => TurnOffAmbientLight());
    }

/// <summary>
/// Controls the wardrobe lighting based on door sensor
/// - Turns on when wardrobe door opens (after 1 second)
/// - Turns off after 5 minutes with door open or 30 minutes after being turned on
/// - Turns off immediately when door closes (after 1 second)
/// </summary>
private void HandleWardrobeLight()
    {
        _entities.BinarySensor.TomasRumDoorSensorWardrobe.StateChanges()
            .WhenStateIsFor(n => n.IsOn(), TimeSpan.FromSeconds(1), _scheduler)
            .Subscribe(_ => _entities.Light.TomasRumLedlampaGarderob.TurnOn());

        _entities.BinarySensor.TomasRumDoorSensorWardrobe.StateChanges()
            .WhenStateIsFor(n => n.IsOn(), TimeSpan.FromMinutes(5), _scheduler)
            .Subscribe(_ => _entities.Light.TomasRumLedlampaGarderob.TurnOff());

        _entities.Light.TomasRumLedlampaGarderob.StateChanges()
            .WhenStateIsFor(n => n.IsOn(), TimeSpan.FromMinutes(30), _scheduler)
            .Subscribe(_ => _entities.Light.TomasRumLedlampaGarderob.TurnOff());

        _entities.BinarySensor.TomasRumDoorSensorWardrobe.StateChanges()
            .WhenStateIsFor(n => n.IsOff(), TimeSpan.FromSeconds(1), _scheduler)
            .Subscribe(_ => _entities.Light.TomasRumLedlampaGarderob.TurnOff());
    }
/// <summary>
/// Controls the air filter based on 3D printer status
/// - Turns on air filter when printer is running
/// - Turns off air filter 5 minutes after printer stops running
/// </summary>
private void HandleAirFilter()
    {
        _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
            .Where(n => n.New?.State == "running")
            .Subscribe(_ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOn());

        _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
            .WhenStateIsFor(n => n?.State != "running" && _entities.Switch.TomasRumGarderobLuftfilter.IsOn(), TimeSpan.FromMinutes(5), _scheduler)
            .Subscribe(_ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOff());
    }

/// <summary>
/// Monitors humidity levels in the filament storage box
/// - Triggers alerts via TTS and mobile notification when humidity exceeds 25% for 1 hour
/// - Creates a persistent notification with current humidity level
/// </summary>
private void HandleFilamentMoustiureLevelAlert()
    {
        _entities.Sensor.TomasRumFilamentBoxTempHumidity.StateChanges()
            .Where(n => n.Old?.State <= 25.0)
            .WhenStateIsFor(n =>  n?.State > 25.0, TimeSpan.FromHours(1), _scheduler)
            .Subscribe(s =>
            {
                _tts.Speak("media_player.huset", "Fukt i filamentboxen är för hög", "cloud_say");
                _services.Notify.MobileAppSmG986b("Fukt i filamentboxen är för hög", "Varning! Filamentboxen har för hög funktighet");
                _services.PersistentNotification.Dismiss("humitidy_notification");
                _services.PersistentNotification.Create($"Varning! Filamentboxen har för hög fuktighet på {s.New.State}", "Hög fuktighet i filamentboxen!", "humitidy_notification");
            });
    }

/// <summary>
/// Turns on the ambient lighting in the room
/// - Activates the main room light
/// - Sets the bookshelf LED to blue with specific brightness
/// </summary>
private void TurnOnAmbientLight()
    {
        _entities.Light.TomasRum.TurnOn();
        _entities.Light.TomasRumLedBokhylla.TurnOn(rgbColor: DeskSceneColors.NotAtDesk, brightness: 150, transition: 0);
    }

/// <summary>
/// Turns off all ambient lighting in the room
/// - Deactivates the main room light
/// - Turns off the bookshelf LED light
/// </summary>
private void TurnOffAmbientLight()
    {
        _entities.Light.TomasRum.TurnOff();
        _entities.Light.TomasRumLedBokhylla.TurnOff();
    }
}

/// <summary>
/// Static class providing RGB color values for different desk lighting scenes
/// </summary>
public static class DeskSceneColors
{
/// <summary>
/// Warm white color (255, 228, 205) used when someone is sitting at the desk
/// </summary>
public static int[] AtDesk => [255, 228, 205];
/// <summary>
/// Blue color (0, 0, 255) used when someone is in the room but not at the desk
/// </summary>
public static int[] NotAtDesk => [0, 0, 255];
/// <summary>
/// White color (255, 255, 255) used as default/fallback when no one is in the room
/// </summary>
public static int[] NotInRoom => [255, 255, 255];
}

/// <summary>
/// Extension methods for IObservable to enhance reactive programming capabilities
/// </summary>
public static class ObservableExtensions
{

/// <summary>
/// Extension method for observables combining two binary sensor state changes
/// - Emits when the combined state of both sensors matches the predicate for the specified time duration
/// - Handles throttling and completion states to ensure proper event timing
/// </summary>
/// <param name="observable">The source observable combining two state changes</param>
/// <param name="predicate">Function determining when the combined state is considered active</param>
/// <param name="timeSpan">Duration the state must remain active before emitting</param>
/// <param name="scheduler">Scheduler used for timing operations</param>
/// <returns>Observable that emits when conditions have been met for specified duration</returns>
public static IObservable<(StateChange<BinarySensorEntity, EntityState<BinarySensorAttributes>>, StateChange<BinarySensorEntity, EntityState<BinarySensorAttributes>>)> WhenCombinedStateIsFor(
        this IObservable<(StateChange<BinarySensorEntity, EntityState<BinarySensorAttributes>>, StateChange<BinarySensorEntity, EntityState<BinarySensorAttributes>>)> observable,
        Func<EntityState?, EntityState?, bool> predicate,
        TimeSpan timeSpan,
        IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(observable, nameof(observable));
        ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
        ArgumentNullException.ThrowIfNull(scheduler, nameof(scheduler));

        var isCompleted = false;

        return observable
            .Do(_ => { }, () => isCompleted = true)
            // Only process changes that start or stop matching the predicate
            .Where(e => predicate(e.Item1.Old, e.Item2.Old) != predicate(e.Item1.New, e.Item2.New))

            // Both  will restart the timer
            .Throttle(timeSpan, scheduler)

            // But only when the new state matches the predicate we emit it
            .Where(e => predicate(e.Item1.New, e.Item2.New) && isCompleted == false);
    }
}

