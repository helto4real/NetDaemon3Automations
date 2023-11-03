// namespace daemonapp.apps.netdaemon3.Media;

[NetDaemonApp]
// [Focus]
public class SpotifyManager
{
    private readonly SpotcastServices _spotcastService;
    private readonly MediaPlayerEntity _player;
    private readonly ILogger<SpotifyManager> _logger;
    private readonly NumericSensorEntity _cubeSideSensor;

    public SpotifyManager(Entities entities, Services services, ILogger<SpotifyManager> logger)
    {
        _spotcastService = services.Spotcast;
        _player = entities.MediaPlayer.Kok;
        _logger = logger;
        _cubeSideSensor = new NumericSensorEntity(entities.Sensor.KokCubeSide);

        _cubeSideSensor.StateChanges()
            .Subscribe(s =>
            {
                if (s.Old?.State is null)
                    return;
                HandleNewSide(s.New?.State);
            });

        entities.Sensor.KokCubeAction.StateChanges()
            .Where(e => e.New?.State == "slide" || e.New?.State == "shake")
            .Subscribe(s =>
            {
                HandleCubeAction(s.New?.State, s.New?.LastChanged - s.Old?.LastChanged, s.New?.Attributes?.Side);
            });
    }

    private void HandleCubeAction(string? newState, TimeSpan? timeDiff, double? side)
    {
        if (side is null)
            return;

        if (timeDiff is {TotalSeconds: >= 2})
        {
            switch (_player.State)
            {
                case "playing":
                    if (Equals(side, _cubeSideSensor?.State))
                    {
                        _player.MediaPause();
                    }
                    else
                    {
                        HandleNewSide(side);
                    }
                    break;
                case "paused":
                    if (Equals(side, _cubeSideSensor?.State))
                    {
                        _player.MediaPlay();
                    }
                    else
                    {
                        HandleNewSide(side);
                    }
                    break;

                default:

                    HandleNewSide(side);
                    break;

            }
        }


    }

    private void HandleNewSide(double? newState)
    {
        switch (newState)
        {
            case 0:
                _spotcastService.Start(entityId: "media_player.kok", uri: "spotify:playlist:37i9dQZF1DX6z20IXmBjWI", randomSong: true, shuffle: true);
                break;
            case 5:
                _spotcastService.Start(entityId: "media_player.kok", uri: "spotify:playlist:37i9dQZF1DX9j444F9NCBa", randomSong: true, account: "elin", shuffle: true);
                break;
            case 4:
                // P1
                _player.PlayMedia(mediaContentType: "music", mediaContentId: "http://sverigesradio.se/topsy/direkt/132-hi.mp3");
                break;
            case 2:
                // P4 Västernorrland
                _player.PlayMedia(mediaContentType: "music", mediaContentId: "http://sverigesradio.se/topsy/direkt/216-hi.mp3");
                break;
        }
    }
}
