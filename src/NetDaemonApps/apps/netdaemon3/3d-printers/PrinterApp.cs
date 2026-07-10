using System.Reactive.Concurrency;

namespace NetDaemonApps;

public sealed class PrinterConfig
{
    public double MaximumFilamentHumidity { get; init; } = 25;

    public TimeSpan HumidityAlertDelay { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan A1FilterStopDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan H2dFilterStopDelay { get; init; } = TimeSpan.FromMinutes(25);

    public string AnnouncementMediaPlayer { get; init; } = "media_player.huset";

    public string TextToSpeechEngine { get; init; } = "cloud_say";
}

[NetDaemonApp]
public sealed class PrinterApp : ReactiveApp, IAsyncInitializable
{
    private const string HumidityNotificationId = "humidity_notification";

    private readonly Entities _entities;
    private readonly IScheduler _scheduler;
    private readonly ITextToSpeechService _textToSpeech;
    private readonly Services _services;
    private readonly ILogger<PrinterApp> _logger;
    private readonly PrinterConfig _config;

    public PrinterApp(
        Entities entities,
        Services services,
        ITextToSpeechService textToSpeech,
        IScheduler scheduler,
        ILogger<PrinterApp> logger,
        IAppConfig<PrinterConfig> config)
    {
        _entities = entities;
        _scheduler = scheduler;
        _textToSpeech = textToSpeech;
        _services = services;
        _logger = logger;
        _config = config.Value;

        ConfigurationValidation.Finite(
            _config.MaximumFilamentHumidity,
            nameof(PrinterConfig.MaximumFilamentHumidity));
        if (_config.MaximumFilamentHumidity < 0)
        {
            throw new InvalidOperationException("Maximum filament humidity must not be negative.");
        }

        ConfigurationValidation.Positive(_config.HumidityAlertDelay, nameof(PrinterConfig.HumidityAlertDelay));
        ConfigurationValidation.Positive(_config.A1FilterStopDelay, nameof(PrinterConfig.A1FilterStopDelay));
        ConfigurationValidation.Positive(_config.H2dFilterStopDelay, nameof(PrinterConfig.H2dFilterStopDelay));
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.AnnouncementMediaPlayer);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.TextToSpeechEngine);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        InitializeAirFilter();
        InitializeHumidityAlert();
    }, cancellationToken);

    private void InitializeAirFilter()
    {
        SubscribeLogged(
            _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
                .Where(change => change.New?.State == HomeAssistantStates.Running),
            _logger,
            "start A1 air filter",
            _ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOn());

        SubscribeLogged(
            _entities.Sensor.A103919c452210211PrintStatus.StateChanges()
                .WhenStateIsFor(
                    state => state?.State != HomeAssistantStates.Running &&
                             _entities.Switch.TomasRumGarderobLuftfilter.IsOn(),
                    _config.A1FilterStopDelay,
                    _scheduler),
            _logger,
            "stop A1 air filter",
            _ => _entities.Switch.TomasRumGarderobLuftfilter.TurnOff());

        SubscribeLogged(
            _entities.Sensor.H2dPrintStatus.StateChanges()
                .Where(change => change.New?.State == HomeAssistantStates.Running),
            _logger,
            "start H2D air filter",
            _ => ApplyH2dFanSpeed());

        SubscribeLogged(
            _entities.Sensor.H2dPrintStatus.StateChanges()
                .WhenStateIsFor(
                    state => state?.State != HomeAssistantStates.Running && _entities.Fan.GymLuftrenare.IsOn(),
                    _config.H2dFilterStopDelay,
                    _scheduler),
            _logger,
            "stop H2D air filter",
            _ => _entities.Fan.GymLuftrenare.TurnOff());

        SubscribeLogged(
            _entities.Sensor.H2d0948bb4c2200520ActiveTray.StateChanges()
                .Where(_ => _entities.Sensor.H2dPrintStatus.State == HomeAssistantStates.Running),
            _logger,
            "update H2D air filter for filament",
            _ => ApplyH2dFanSpeed());
    }

    private void InitializeHumidityAlert()
    {
        SubscribeLogged(
            _entities.Sensor.TomasRumFilamentBoxTempHumidity.StateChanges()
                .Where(change => change.Old?.State <= _config.MaximumFilamentHumidity)
                .WhenStateIsFor(
                    state => state?.State > _config.MaximumFilamentHumidity,
                    _config.HumidityAlertDelay,
                    _scheduler),
            _logger,
            "alert about filament humidity",
            change =>
            {
                var humidity = change.New?.State;
                _textToSpeech.Speak(
                    _config.AnnouncementMediaPlayer,
                    "Fukt i filamentboxen är för hög",
                    _config.TextToSpeechEngine);
                _services.Notify.MobileAppSmG986b(
                    "Fukt i filamentboxen är för hög",
                    "Varning! Filamentboxen har för hög fuktighet");
                _services.PersistentNotification.Dismiss(HumidityNotificationId);
                _services.PersistentNotification.Create(
                    $"Varning! Filamentboxen har för hög fuktighet på {humidity}",
                    "Hög fuktighet i filamentboxen!",
                    HumidityNotificationId);
            });
    }

    private void ApplyH2dFanSpeed()
    {
        var percentage = PrinterVentilationPolicy.GetPrinterFanPercentage(
            _entities.Sensor.H2dPrintStatus.State,
            _entities.Sensor.H2d0948bb4c2200520ActiveTray.State);

        if (percentage is not null)
        {
            _entities.Fan.GymLuftrenare.TurnOn(percentage: percentage);
        }
    }
}
