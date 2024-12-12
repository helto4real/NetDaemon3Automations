using System;
using System.Reactive.Concurrency;
using System.Text.Json;

[NetDaemonApp]
// [Focus]
public class TomasOfficeApp
{
    private readonly Entities _entities;
    private readonly IScheduler _scheduler;
    private readonly ITextToSpeechService _tts;
    private readonly IServices _services;
    private readonly ILogger<TomasOfficeApp> _logger;

    public TomasOfficeApp(Entities entities, Services services, ITextToSpeechService tts, IScheduler scheduler, ILogger<TomasOfficeApp> logger)
    {
        _entities = entities;
        _scheduler = scheduler;
        _tts = tts;
        _services = services;
        _logger = logger;

        HandlebBlinds();
        HandleDeskRgbLightsOnPresence();
        HandleKeyLightWhenSittingAtDesk();
        HandleAmbientDependingOnBlindState();
        HandleAmbientWhenPeopleAreInTheRoomDuringNighttime();
        HandleAmbientWhenPeopleAreInTheRoomDuringDayAndMorningWhenBlindsAreDown();
        HandleWardrobeLight();
        HandleFilamentMoustiureLevelAlert();
        HandleAirFilter();
    }

    private void HandlebBlinds()
    {
        _entities.Button.TomsaRumKnappRullgardin.StateAllChanges()
            .Where(n => n.New?.Attributes?.Action is not null)
            .Subscribe(s =>
        {
            var action = ((JsonElement)s.New!.Attributes!.Action!).GetString();
            switch (action)
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

    private bool IsNighttime => _entities.InputSelect.HouseModeSelect.State == "Natt";
    private bool IsEveningtime => _entities.InputSelect.HouseModeSelect.State == "Kväll";
    private bool SomeoneIsInRoom => _entities.BinarySensor.TomasRumEprlZone2Occupancy.State == "on" || _entities.BinarySensor.TomasRumEprlZone1Occupancy.State == "on";

    private void HandleAmbientDependingOnBlindState()
    {
        _entities.Cover.TomasRumRullgardin.StateChanges()
            .Where(n => n.New?.State == "closed" && SomeoneIsInRoom)
            .Subscribe(s => TurnOnAmbientLight());

        _entities.Cover.TomasRumRullgardin.StateChanges()
           .Where(n => n.New?.State == "open" && !IsNighttime && !IsEveningtime)
           .Subscribe(s => TurnOffAmbientLight());
    }

    private void HandleDeskRgbLightsOnPresence()
    {
        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n => n.zone1.New?.State == "on" || n.zone2.New?.State == "on")
            .Subscribe(s =>
                    {
                        var rgb = (s.zone1.New?.State, s.zone2.New?.State) switch
                        {
                            (_, "on") => DeskSceneColors.AtDesk,
                            (_, "off") => DeskSceneColors.NotAtDesk,
                            _ => DeskSceneColors.NotInRoom
                        };
                        // _logger.LogInformation("Turning on rgb RGB: {0}", rgb);
                        if (rgb != DeskSceneColors.NotInRoom)
                        {
                            _entities.Light.TomasRumLedSkrivbord.TurnOn(rgbColor: rgb);
                        }
                    });

        _entities.BinarySensor.TomasRumEprlZone1Occupancy.StateChanges()
            .CombineLatest(_entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges(), (zone1, zone2) => (zone1, zone2))
            .Where(n => n.zone2.New?.State == "off" && n.zone1.New?.State == "off")
            .Subscribe(_ =>
                    {
                        _entities.Light.TomasRumLedSkrivbord.TurnOff();
                    });

    }

    private void HandleKeyLightWhenSittingAtDesk()
    {
        _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
            .Where(n => n.New?.State == "on" && _entities.Light.ElgatoKeyLight.State == "off")
            .Subscribe(_ => _entities.Light.ElgatoKeyLight.TurnOn());

        _entities.BinarySensor.TomasRumEprlZone2Occupancy.StateChanges()
            .WhenStateIsFor(n => n?.State == "off" && _entities.Light.ElgatoKeyLight.State == "on", TimeSpan.FromMinutes(20), _scheduler)
            .Subscribe(_ => _entities.Light.ElgatoKeyLight.TurnOff());
    }

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
    private void HandleAirFilter()
    {
        _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
            .Where(n => n.New?.State == "running")
            .Subscribe(_ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOn());

        _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
            .WhenStateIsFor(n => n?.State != "running" && _entities.Switch.TomasRumGarderobLuftfilter.IsOn(), TimeSpan.FromMinutes(5), _scheduler)
            .Subscribe(_ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOff());
    }

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
                _services.PersistentNotification.Create("Varning! Filamentboxen har för hög fuktighet på {3}", "Hög fuktighet i filamentboxen!", "humitidy_notification");
            });
    }

    private void TurnOnAmbientLight()
    {
        _entities.Light.TomasRum.TurnOn();
        _entities.Light.TomasRumLedBokhylla.TurnOn(rgbColor: DeskSceneColors.NotAtDesk, brightness: 150, transition: 0);
    }
    private void TurnOffAmbientLight()
    {
        _entities.Light.TomasRum.TurnOff();
        _entities.Light.TomasRumLedBokhylla.TurnOff();
    }
}

public static class DeskSceneColors
{
    public static int[] AtDesk => [255, 228, 205];
    public static int[] NotAtDesk => [0, 0, 255];
    public static int[] NotInRoom => [255, 255, 255];
}

public static class ObservableExtensions
{

    // public static IObservabe<StateChange<TEntity, TEntityState>> WhenCombinedStateIsFor<TEntity, TEntityState>(
    public static IObservable<(StateChange<BinarySensorEntity, EntityState<BinarySensorAttributes>>, StateChange<BinarySensorEntity, EntityState<BinarySensorAttributes>>)> WhenCombinedStateIsFor(
        // this IObservable<(StateChange , StateChange)> observable,
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

