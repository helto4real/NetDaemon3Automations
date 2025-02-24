// namespace daemonapp.apps.netdaemon3.Media;

using System.Text.Json.Serialization;

[NetDaemonApp]
//[Focus]
public class SpotifyManagerNew
{
    private readonly SpotcastServices _spotcastService;
    private readonly MediaPlayerEntity _player;
    private readonly MediaPlayerEntity _spotifyTomas;
    private readonly MediaPlayerEntity _spotifyElin;
    private readonly Services _services;
    private readonly ILogger<SpotifyManagerNew> _logger;
    private readonly ITriggerManager _triggerManager;
    private readonly NumericSensorEntity _cubeSideSensor;

    public SpotifyManagerNew(Entities entities, Services services, ILogger<SpotifyManagerNew> logger, ITriggerManager triggerManager)
    {
        _spotcastService = services.Spotcast;
        _player = entities.MediaPlayer.Kok;
        _spotifyTomas = entities.MediaPlayer.SpotifyTomasHellstrom;
        _spotifyElin = entities.MediaPlayer.SpotifyElinHellstrom;
        _services = services;

        _logger = logger;
        _triggerManager = triggerManager;
        _cubeSideSensor = new NumericSensorEntity(entities.Sensor.KokCubeSide);


        triggerManager.RegisterMqttActionTrigger("kok_cube")
            .Where(e => e == "slide" || e == "shake")
            .Subscribe(_ =>
            {

                HandleCubeAction(_cubeSideSensor.State);
            });
        _cubeSideSensor.StateChanges()
            .Subscribe(s =>
            {
                if (s.Old?.State is null)
                    return;
                HandleNewSide(s.New?.State);
            });
        /**/
        /*entities.Sensor.KokCubeSide.StateChanges()*/
        /*    .Where(e => e.New?.State == "slide" || e.New?.State == "shake")*/
        /*    .Subscribe(s =>*/
        /*    {*/
        /*        HandleCubeAction(s.New?.State, s.New?.LastChanged - s.Old?.LastChanged, s.New?.Attributes?.CurrentSide);*/
        /*    });*/
    }

    private void HandleCubeAction(double? side)
    {
        if (side is null)
            return;

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

    private void HandleNewSide(double? newState)
    {
        switch (newState)
        {
            case 0:
                _spotifyTomas.SelectSource("Kök");
                _spotifyTomas.PlayMedia("https://open.spotify.com/playlist/0ynJQD5wQwjVqvaxae6nMM?si=1f0708287d904806", "playlist");
                break;
            case 5:
                _spotifyTomas.SelectSource("Kök");
                _spotifyTomas.PlayMedia("https://open.spotify.com/playlist/0ynJQD5wQwjVqvaxae6nMM?si=1f0708287d904806", "playlist");
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
public record MediaParameters : MediaPlayerPlayMediaParameters
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;


}
