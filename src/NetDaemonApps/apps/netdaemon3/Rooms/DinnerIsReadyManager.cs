/// <summary>
/// Manages dinner ready announcements.
/// </summary>
[NetDaemonApp]
public class DinnerIsReadyManager
{
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly Random _randomizer = new();
    private IReadOnlyCollection<string> _messages;
/// <summary>
/// Initializes a new instance of the <see cref="DinnerIsReadyManager"/> class.
/// </summary>
/// <param name="entities">The entities manager.</param>
/// <param name="textToSpeechService">The text-to-speech service.</param>
/// <param name="messages">The dinner ready messages.</param>
/// <param name="logger">The logger for diagnostic information.</param>
    public DinnerIsReadyManager(
        Entities entities,
        ITextToSpeechService textToSpeechService,
        IAppConfig<DinnerIsReadyMessages> messages,
        ILogger<DinnerIsReadyManager> logger)
    {
        _textToSpeechService = textToSpeechService;
        _messages = messages.Value;
        // States can be open/close/None/stop
        // entities.Sensor.KnappKokAction.StateChanges()
        //     .Where(n => n.ButtonIsClicked())
        //     .Subscribe(s => BroadcastDinnerIsReady());
    }

    private void BroadcastDinnerIsReady()
    {
        _textToSpeechService.Speak(
            "media_player.huset",
            GetRandomMessage(), "google_cloud_say");
    }

    private string GetRandomMessage()
    {
        var randomMessageIndex = _randomizer.Next(0, _messages.Count - 1);
        return _messages.ElementAt(randomMessageIndex);
    }
}

/// <summary>
/// Represents a collection of dinner ready messages.
/// </summary>
public class DinnerIsReadyMessages : List<string> { }
