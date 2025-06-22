
using System.Reactive.Concurrency;
using System.Text.Json;

/// <summary>
/// Automation app for Tomas' office managing all automations related to this room
/// including lighting, blinds, climate control and device management
/// </summary>
[NetDaemonApp]
// [Focus]
public class PrinterApp
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
    public PrinterApp(Entities entities, Services services, ITriggerManager triggerManager, ITextToSpeechService tts, IScheduler scheduler, ILogger<TomasOfficeApp> logger)
    {
        _entities = entities;
        _scheduler = scheduler;
        _tts = tts;
        _services = services;
        _triggerManager = triggerManager;
        HandleFilamentMoustiureLevelAlert();
        HandleAirFilter();
    }
    /// <summary>
    /// Controls the air filter based on 3D printer status
    /// - Turns on air filter when printer is running
    /// - Turns off air filter 5 minutes after printer stops running
    /// </summary>
    private void HandleAirFilter()
    {
        // Handle filter for A1
        _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
            .Where(n => n.New?.State == "running")
            .Subscribe(_ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOn());

        _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
            .WhenStateIsFor(n => n?.State != "running" && _entities.Switch.TomasRumGarderobLuftfilter.IsOn(), TimeSpan.FromMinutes(5), _scheduler)
            .Subscribe(_ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOff());

        // Handle filter for H2D
        _ = _entities.Sensor.H2dPrintStatus.StateChanges()
            .Where(n => n.New?.State == "running")
            .Subscribe(_ =>
                    {
                        // Check if the ActiveTray sensor contains ASA or ABS filament type
                        var activeTrayState = _entities.Sensor.H2d0948bb4c2200520ActiveTray.State;
                        var stateStr = activeTrayState?.ToString().ToLower() ?? "";

                        if (stateStr.Contains("asa") || stateStr.Contains("abs"))
                        {
                            _entities.Fan.GymLuftrenare.TurnOn(percentage: 75);
                        }
                        else
                        {
                            _entities.Fan.GymLuftrenare.TurnOn(percentage: 20);
                        }
                    });

        _entities.Sensor.H2dPrintStatus.StateChanges()
            .WhenStateIsFor(n => n?.State != "running" && _entities.Fan.GymLuftrenare.IsOn(), TimeSpan.FromMinutes(25), _scheduler)
            .Subscribe(_ => _entities.Fan.GymLuftrenare.TurnOff());

        _entities.Sensor.H2d0948bb4c2200520ActiveTray.StateChanges()
            .Subscribe(s =>
            {
                var activeTray = s.New?.State?.ToString().ToLower() ?? "";
                if (activeTray.Contains("asa") || activeTray.Contains("abs"))
                {
                    _entities.Fan.GymLuftrenare.TurnOn(percentage: 75);
                }
            });
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
            .WhenStateIsFor(n => n?.State > 25.0, TimeSpan.FromHours(1), _scheduler)
            .Subscribe(s =>
            {
                _tts.Speak("media_player.huset", "Fukt i filamentboxen är för hög", "cloud_say");
                _services.Notify.MobileAppSmG986b("Fukt i filamentboxen är för hög", "Varning! Filamentboxen har för hög funktighet");
                _services.PersistentNotification.Dismiss("humitidy_notification");
                _services.PersistentNotification.Create($"Varning! Filamentboxen har för hög fuktighet på {s.New.State}", "Hög fuktighet i filamentboxen!", "humitidy_notification");
            });
    }

}
