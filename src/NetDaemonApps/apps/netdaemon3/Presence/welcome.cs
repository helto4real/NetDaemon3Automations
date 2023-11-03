using NetDaemon.Extensions.Tts;

public class WelcomeConfig
{
    /// <summary>
    ///     Used to search the correct device trackers by naming
    /// </summary>
    public string? PresenceCriteria { get; set; }

    public MediaPlayerEntity? HallwayMediaPlayer { get; set; }

    public BinarySensorEntity? DoorSensor { get; set; }

    public IEnumerable<string>? Greetings { get; init; }
}

/// <summary>
///     Greets (or insults) people when coming home :)
/// </summary>
[NetDaemonApp]
public class WelcomeHomeManager
{
    private readonly WelcomeConfig _config;
    private readonly IHaContext _ha;
    private readonly Dictionary<string, DateTime> _lastTimeGreeted = new(5);
    private readonly Random _randomizer = new();
    private readonly ITextToSpeechService _tts;
    private readonly ILogger<WelcomeHomeManager> _logger;

    public WelcomeHomeManager(
        IHaContext haContext,
        ILogger<WelcomeHomeManager> logger,
        ITextToSpeechService textToSpeechService,
        IAppConfig<WelcomeConfig> config)
    {
        _ha = haContext;
        _tts = textToSpeechService;
        _config = config.Value;
        _logger = logger;
        
        ArgumentNullException.ThrowIfNull(_config.Greetings);
        ArgumentNullException.ThrowIfNull(_config.DoorSensor);
        ArgumentNullException.ThrowIfNull(_config.HallwayMediaPlayer);
        ArgumentNullException.ThrowIfNull(_config.PresenceCriteria);
        Intitialize();
    }

    private void Intitialize()
    {
        _config.DoorSensor?.StateChanges()
            .Where(e => e.New.IsOn())
            .Subscribe(s =>
            {
                //_logger.LogInformation("A door sensor changed {From} -> {To}", s.Old, s.New);

                GreetIfJustArrived(s.New?.EntityId);
            });

        // If the person/s has the presence "just arrived"
        _ha.StateChanges()
            .Where(
                e => e.New?.EntityId is not null && e.New.EntityId.EndsWith(_config.PresenceCriteria!) &&
                     e.New?.State == "Nyss anlänt")
            .Subscribe(s =>
            {
                //_logger.LogInformation("A precence changed to 'Just Arrived' {From} -> {To}", s.Old, s.New);
                GreetIfJustArrived(s.New?.EntityId);
            });
    }

    private void GreetIfJustArrived(string? entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);

        if (entityId.StartsWith("binary_sensor."))
        {
            // The door opened, lets check if someone just arrived
            var trackerJustArrived = _ha.GetAllEntities()
                .Where(n => n.EntityId.EndsWith(_config.PresenceCriteria!) && n.State == "Nyss anlänt");

            //_logger.LogInformation("All trackers that is just arrived {Trackers}", trackerJustArrived);
            foreach (var tracker in trackerJustArrived) Greet(tracker.EntityId);
        }
        else if (entityId.StartsWith("device_tracker."))
        {
            var doorSensorState = _config.DoorSensor?.State ?? throw new InvalidOperationException();
            var lastChanged = _config.DoorSensor.EntityState?.LastChanged ?? throw new InvalidOperationException();

            if (doorSensorState == "on")
                // Door is open, greet
                Greet(entityId);
            else if (doorSensorState == "off")
                // It is closed, lets check if it was recently opened
                if (DateTime.Now.Subtract(lastChanged) <= TimeSpan.FromMinutes(5))
                    // It was recently opened, probably when someone got home
                    Greet(entityId);
        }
    }

    private void Greet(string tracker)
    {
        // Get the name from tracker i.e. device_tracker.name_presense
        var nameOfPerson = tracker[15..^_config.PresenceCriteria!.Length];

        if (!OkToGreet(nameOfPerson))
            return; // We can not greet person just yet
        //_logger.LogInformation("Ok to greet {Name}!", nameOfPerson);

        _tts.Speak(_config.HallwayMediaPlayer?.EntityId!, GetGreeting(nameOfPerson), "google_cloud_say");
    }

    private bool OkToGreet(string nameOfPerson)
    {
        if (_lastTimeGreeted.ContainsKey(nameOfPerson) == false)
        {
            _lastTimeGreeted[nameOfPerson] = DateTime.Now;
            return true;
        }

        if (DateTime.Now.Subtract(_lastTimeGreeted[nameOfPerson]).TotalMinutes <= 15)
            return false; // To early to greet again

        _lastTimeGreeted[nameOfPerson] = DateTime.Now;
        return true; // It is ok to greet now
    }

    private string GetGreeting(string name)
    {
        var randomMessageIndex = _randomizer.Next(0, _config.Greetings!.Count() - 1);
        return _config.Greetings!.ElementAt(randomMessageIndex).Replace("{namn}", name);
    }


}
