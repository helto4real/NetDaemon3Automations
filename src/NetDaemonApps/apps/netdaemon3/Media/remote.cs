public class MagicCubeControlConfig
{
    public RemoteEntity? RemoteTVRummet { get; set; }
    public string? MaranzDeviceId { get; set; }
    public IEnumerable<MediaPlayerEntity>? TvMediaPlayers { get; set; }
}

/// <summary>
///     Manage remote control using a xiaomi magic cube.
///     Following use-cases are implemented
///     - Shake, Turns on / Turns off TV
///     - Turn clockwise, volume up
///     - Turn counter clockwise, volume down
///     - Flip, pause/play
/// </summary>
[NetDaemonApp]
// [Focus]
public class MagicCubeRemoteControlManager
{
    private readonly MagicCubeControlConfig _config;

    private readonly Entities _entities;

    public MagicCubeRemoteControlManager(IHaContext ha, IAppConfig<MagicCubeControlConfig> config)
    {
        // _ha = ha;
        _entities = new Entities(ha);
        _config = config.Value;
        Initialize();
    }

    public void Initialize()
    {
        // _entities.Sensor.tv.StateChanges()
        //     .Subscribe(s =>
        //     {
        //         if (s.New?.State is null)
        //             return;
        //
        //         switch (s.New?.State)
        //         {
        //             case "shake": // Shake
        //                 _entities.Remote.Tvrummet.Toggle();
        //                 break;
        //             case "flip90": // Flip
        //                 PlayPauseMedia();
        //                 break;
        //             case "rotate_right": // Turn clockwise
        //                 VolumeUp();
        //                 break;
        //             case "rotate_left": // Turn counter clockwise
        //                 VolumeDown();
        //                 break;
        
    }

    /// <summary>
    ///     Pauses any media playing, play any paused media connected to TV
    /// </summary>
    private void PlayPauseMedia()
    {
        // we are not just using PlayPause service since if the media player is nether we want to ignore it
        foreach (var player in _config.TvMediaPlayers ?? Array.Empty<MediaPlayerEntity>())
        {
            var playerState = player?.State;
            if (playerState == "playing")
                player?.MediaPause();
            else if (playerState == "paused")
                player?.MediaPlay();
        }
    }

    /// <summary>
    ///     Turn volume up on Maranz receiver
    /// </summary>
    private void VolumeUp()
    {
        _config.RemoteTVRummet?.SendCommand(
            device: _config.MaranzDeviceId,
            command: "VolumeUp",
            numRepeats: 10,
            delaySecs: 0.01
        );
    }

    /// <summary>
    ///     Turn volume down on Maranz receiver
    /// </summary>
    private void VolumeDown()
    {
        _config.RemoteTVRummet?.SendCommand(
            device: _config.MaranzDeviceId,
            command: "VolumeDown",
            numRepeats: 10,
            delaySecs: 0.01
        );
    }
}
