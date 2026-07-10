namespace NetDaemonApps;

public sealed class WelcomeConfig
{
    public string PresenceCriteria { get; init; } = string.Empty;

    public MediaPlayerEntity HallwayMediaPlayer { get; init; } = null!;

    public BinarySensorEntity DoorSensor { get; init; } = null!;

    public IReadOnlyList<string> Greetings { get; init; } = [];

    public TimeSpan DoorOpenLookback { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan GreetingCooldown { get; init; } = TimeSpan.FromMinutes(15);

    public string TextToSpeechEngine { get; init; } = "google_translate_say";
}

[NetDaemonApp]
public sealed class WelcomeHomeManager : ReactiveApp, IAsyncInitializable
{
    private const string BinarySensorPrefix = "binary_sensor.";
    private const string DeviceTrackerPrefix = "device_tracker.";

    private readonly WelcomeConfig _config;
    private readonly IHaContext _haContext;
    private readonly Dictionary<string, DateTimeOffset> _lastTimeGreeted = new(StringComparer.Ordinal);
    private readonly ITextToSpeechService _textToSpeech;
    private readonly ILogger<WelcomeHomeManager> _logger;
    private readonly TimeProvider _timeProvider;

    public WelcomeHomeManager(
        IHaContext haContext,
        ILogger<WelcomeHomeManager> logger,
        ITextToSpeechService textToSpeech,
        IAppConfig<WelcomeConfig> config,
        TimeProvider timeProvider)
    {
        _haContext = haContext;
        _textToSpeech = textToSpeech;
        _config = config.Value;
        _logger = logger;
        _timeProvider = timeProvider;

        ValidateConfiguration(_config);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        SubscribeLogged(
            _config.DoorSensor.StateChanges().Where(change => change.New.IsOn()),
            _logger,
            "greet after door opened",
            change => GreetIfJustArrived(change.New?.EntityId));

        SubscribeLogged(
            _haContext.StateChanges().Where(change =>
                change.New?.EntityId is not null &&
                change.New.EntityId.EndsWith(_config.PresenceCriteria, StringComparison.Ordinal) &&
                change.New.State == PresenceStates.JustArrived),
            _logger,
            "greet after presence changed",
            change => GreetIfJustArrived(change.New?.EntityId));

    }, cancellationToken);

    private void GreetIfJustArrived(string? entityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        if (entityId.StartsWith(BinarySensorPrefix, StringComparison.Ordinal))
        {
            var trackers = _haContext.GetAllEntities().Where(entity =>
                entity.EntityId.EndsWith(_config.PresenceCriteria, StringComparison.Ordinal) &&
                entity.State == PresenceStates.JustArrived);

            foreach (var tracker in trackers)
            {
                Greet(tracker.EntityId);
            }

            return;
        }

        if (!entityId.StartsWith(DeviceTrackerPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var doorState = _config.DoorSensor.State;
        var lastChanged = _config.DoorSensor.EntityState?.LastChanged;

        if (doorState == HomeAssistantStates.On ||
            doorState == HomeAssistantStates.Off &&
            lastChanged is not null &&
            _timeProvider.GetLocalNow() - lastChanged.Value <= _config.DoorOpenLookback)
        {
            Greet(entityId);
        }
    }

    private void Greet(string tracker)
    {
        var suffixLength = _config.PresenceCriteria.Length;
        if (tracker.Length <= DeviceTrackerPrefix.Length + suffixLength)
        {
            _logger.LogWarning("Could not extract a person name from tracker {Tracker}", tracker);
            return;
        }

        var name = tracker[DeviceTrackerPrefix.Length..^suffixLength];
        if (!CanGreet(name))
        {
            return;
        }

        _textToSpeech.Speak(
            _config.HallwayMediaPlayer.EntityId,
            GetGreeting(name),
            _config.TextToSpeechEngine);
    }

    private bool CanGreet(string name)
    {
        var now = _timeProvider.GetLocalNow();
        if (_lastTimeGreeted.TryGetValue(name, out var lastGreeting) && now - lastGreeting <= _config.GreetingCooldown)
        {
            return false;
        }

        _lastTimeGreeted[name] = now;
        return true;
    }

    private string GetGreeting(string name)
    {
        var greeting = RandomSelection.SelectRandom(_config.Greetings, Random.Shared);
        return greeting.Replace("{namn}", name, StringComparison.Ordinal);
    }

    private static void ValidateConfiguration(WelcomeConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration.HallwayMediaPlayer);
        ArgumentNullException.ThrowIfNull(configuration.DoorSensor);
        ArgumentNullException.ThrowIfNull(configuration.Greetings);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.PresenceCriteria);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.HallwayMediaPlayer.EntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.DoorSensor.EntityId);
        ConfigurationValidation.NonNegative(
            configuration.DoorOpenLookback,
            nameof(WelcomeConfig.DoorOpenLookback));
        ConfigurationValidation.Positive(
            configuration.GreetingCooldown,
            nameof(WelcomeConfig.GreetingCooldown));
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.TextToSpeechEngine);

        if (configuration.Greetings.Count == 0 || configuration.Greetings.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("At least one non-empty greeting must be configured.");
        }
    }
}
