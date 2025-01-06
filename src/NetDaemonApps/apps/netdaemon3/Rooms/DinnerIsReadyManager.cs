[NetDaemonApp]
public class DinnerIsReadyManager
{
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly Random _randomizer = new();
    private IReadOnlyCollection<string> _messages;
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

public class DinnerIsReadyMessages : List<string> { }
