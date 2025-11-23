using System.Reactive.Concurrency;

namespace NetDaemonApps
{

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
    public TomasOfficeApp(Entities entities, Services services, ITriggerManager triggerManager, ITextToSpeechService tts, IScheduler scheduler)
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

}