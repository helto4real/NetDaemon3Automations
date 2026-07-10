namespace NetDaemonApps;

public sealed class CalendarAnnouncementConfig
{
    public string ImportantEventKeyword { get; init; } = "sopor";

    public string MediaPlayer { get; init; } = "media_player.huset";

    public string TextToSpeechEngine { get; init; } = "cloud_say";
}

[NetDaemonApp]
public sealed class GoogleCalendarManager : ReactiveApp, IAsyncInitializable
{
    private readonly Entities _entities;
    private readonly ITextToSpeechService _textToSpeech;
    private readonly ILogger<GoogleCalendarManager> _logger;
    private readonly CalendarAnnouncementConfig _config;

    public GoogleCalendarManager(
        Entities entities,
        ITextToSpeechService textToSpeech,
        ILogger<GoogleCalendarManager> logger,
        IAppConfig<CalendarAnnouncementConfig> config)
    {
        _entities = entities;
        _textToSpeech = textToSpeech;
        _logger = logger;
        _config = config.Value;

        ArgumentException.ThrowIfNullOrWhiteSpace(_config.ImportantEventKeyword);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.MediaPlayer);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.TextToSpeechEngine);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        Track(_entities.Calendar.FamiljenHellstrom.WhenTurnsOn(change =>
        {
            var attributes = change.New?.Attributes;
            var message = attributes?.Message;
            _logger.LogInformation("Calendar event: {Event}", message);

            if (message?.Contains(_config.ImportantEventKeyword, StringComparison.OrdinalIgnoreCase) != true)
            {
                return;
            }

            _textToSpeech.Speak(_config.MediaPlayer, "Viktigt meddelande", _config.TextToSpeechEngine);
            _textToSpeech.Speak(_config.MediaPlayer, message, _config.TextToSpeechEngine);

            if (!string.IsNullOrWhiteSpace(attributes?.Description))
            {
                _textToSpeech.Speak(_config.MediaPlayer, attributes.Description, _config.TextToSpeechEngine);
            }
        }));

    }, cancellationToken);
}
