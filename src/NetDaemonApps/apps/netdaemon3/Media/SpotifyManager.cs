namespace NetDaemonApps;

public sealed class SpotifyConfig
{
    public string PlaybackSource { get; init; } = "Kök";

    public string TomasPlaylist { get; init; } = "https://open.spotify.com/playlist/0ynJQD5wQwjVqvaxae6nMM";

    public string RadioP1Stream { get; init; } = "https://sverigesradio.se/topsy/direkt/132-hi.mp3";

    public string RadioP4Stream { get; init; } = "https://sverigesradio.se/topsy/direkt/216-hi.mp3";

    public string CubeMqttDevice { get; init; } = "kok_cube";
}

[NetDaemonApp]
public sealed class SpotifyManager : ReactiveApp, IAsyncInitializable
{
    private readonly MediaPlayerEntity _player;
    private readonly MediaPlayerEntity _spotifyTomas;
    private readonly ITriggerManager _triggerManager;
    private readonly NumericSensorEntity _cubeSideSensor;
    private readonly ILogger<SpotifyManager> _logger;
    private readonly SpotifyConfig _config;

    public SpotifyManager(
        Entities entities,
        ITriggerManager triggerManager,
        ILogger<SpotifyManager> logger,
        IAppConfig<SpotifyConfig> config)
    {
        _player = entities.MediaPlayer.Kok;
        _spotifyTomas = entities.MediaPlayer.SpotifyTomasHellstrom;
        _triggerManager = triggerManager;
        _cubeSideSensor = new NumericSensorEntity(entities.Sensor.KokCubeSide);
        _logger = logger;
        _config = config.Value;

        ArgumentException.ThrowIfNullOrWhiteSpace(_config.PlaybackSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.TomasPlaylist);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.RadioP1Stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.RadioP4Stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.CubeMqttDevice);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        SubscribeLogged(
            _triggerManager.RegisterMqttActionTrigger(_config.CubeMqttDevice)
                .Where(action => action is "slide" or "shake"),
            _logger,
            "handle Spotify cube gesture",
            _ => HandleCubeGesture(_cubeSideSensor.State));

        SubscribeLogged(
            _cubeSideSensor.StateChanges().Where(change => change.Old?.State is not null),
            _logger,
            "handle Spotify cube side",
            change => HandleNewSide(change.New?.State));

    }, cancellationToken);

    private void HandleCubeGesture(double? side)
    {
        if (side is null)
        {
            return;
        }

        switch (_player.State)
        {
            case HomeAssistantStates.Playing:
                _player.MediaPause();
                break;
            case HomeAssistantStates.Paused:
                _player.MediaPlay();
                break;
            default:
                HandleNewSide(side);
                break;
        }
    }

    private void HandleNewSide(double? side)
    {
        switch (side)
        {
            case 0:
            case 5:
                _spotifyTomas.SelectSource(_config.PlaybackSource);
                _spotifyTomas.PlayMedia(_config.TomasPlaylist, "playlist");
                break;
            case 4:
                _player.PlayMedia(new
                {
                    media_content_type = "music",
                    media_content_id = _config.RadioP1Stream,
                });
                break;
            case 2:
                _player.PlayMedia(new
                {
                    media_content_type = "music",
                    media_content_id = _config.RadioP4Stream,
                });
                break;
        }
    }
}
