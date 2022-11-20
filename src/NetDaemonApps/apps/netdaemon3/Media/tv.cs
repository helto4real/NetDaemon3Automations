//

using System;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using NetDaemon.HassModel;

/// <summary>
///     Manage the media in the tv room
///     Following use-cases are implemented
///     - Turn on TV and set scene when chromecast is playing and TV is off
///     - Turn off TV when nothing is playing for a time
///     - When remote activity changes, run correct scene (RunScript)
/// </summary>
[NetDaemonApp]
// [Focus]
public class TVManager
{
    private readonly Entities _entities;
    private readonly IHaContext _ha;

    // 20 minutes idle before turn off TV
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(20);
    private readonly ILogger<TVManager> _log;
    private readonly IScheduler _scheduler;

    private readonly Services _services;

    // If this RunScript paused the mediaplayer, it is here
    // private MediaPlayerEntity? _currentlyPausedMediaPlayer;

    // True if we are in the process of turning on the TV
    // private bool _isTurningOnTv;

    // The time when we stopped play media for any of the media players
    // private DateTime? _timeStoppedPlaying;

    public TVManager(IHaContext ha, ILogger<TVManager> logger, IScheduler scheduler)
    {
        _ha = ha;
        _entities = new Entities(ha);
        _services = new Services(ha);
        _log = logger;
        _scheduler = scheduler;

        // _entities.MediaPlayer.ShieldTv.StateChanges()
        //     .StartWith(new StateChange(_entities.MediaPlayer.ShieldTv, null, _entities.MediaPlayer.ShieldTv.EntityState))
        //     .CombineLatest(
        //         _entities.MediaPlayer.TvNere.StateChanges()
        //        .StartWith(new StateChange(_entities.MediaPlayer.TvNere, null, _entities.MediaPlayer.TvNere.EntityState))
        //     )
        //     .Where(n => n.First.New?.State == "playing" || n.Second.New?.State == "playing")
        //     .Subscribe(_ => _log.LogInformation())

        // Turn off TV-system if samsung TV is off more than 10 seconds
        _entities.MediaPlayer.SamsungQ60Series65
            .StateChanges()
            .WhenStateIsFor(n => n.IsOff(), TimeSpan.FromSeconds(10), _scheduler)
            .Subscribe(_ => _entities.Remote.Tvrummet.TurnOff());

        // _entities.MediaPlayer.TvNere
        //     .StateChanges()
        //     .Subscribe(s => { OnMediaStateChanged(s.New, s.Old); });

        // When TV on (remote on), call OnTvTurnedOn
        // _entities.Remote.Tvrummet
        //     .StateChanges()
        //     .Where(e =>
        //         e.New.IsOn())
        //     .SubscribeAsync(async s => { await OnTVTurnedOn().ConfigureAwait(false); });

        // _entities.Remote.Tvrummet
        //     .StateChanges()
        //     .Where(e =>
        //         e.New.IsOff())
        //     .SubscribeAsync(async s => { await HandleOnTvOff().ConfigureAwait(false); });


        // When ever TV remote activity changes, ie TV, Film, Poweroff call OnTvActivityChange
        _entities.Remote.Tvrummet
            .StateAllChanges()
            .Where(e => e.New?.Attributes?.CurrentActivity != e.Old?.Attributes?.CurrentActivity)
            .SubscribeAsync(async s =>
            {
                _log.LogDebug("TV remote activity change from {From} to {To}", s.Old?.Attributes?.CurrentActivity,
                    s.New?.Attributes?.CurrentActivity);
                switch (s.New?.Attributes?.CurrentActivity)
                {
                    case "TV":
                    case "Film":
                        await HandleOnTvOn().ConfigureAwait(false);
                        break;
                    case "PowerOff":
                        await HandleOnTvOff().ConfigureAwait(false);
                        break;
                }
            });
    }

    /// <summary>
    ///     Returns true if it is currently night
    /// </summary>
    private bool IsNight => _entities.InputSelect.HouseModeSelect?.State == "Natt";

    /// <summary>
    ///     Returns true if it is currently evening
    /// </summary>
    private bool IsEvening => _entities.InputSelect.HouseModeSelect?.State == "Kväll";

    /// <summary>
    ///     Returns true if TV is currently on
    /// </summary>
    private bool TvIsOn => _entities.Remote.Tvrummet.IsOn();

    /// <summary>
    ///     Returns true if any of the media players is playing
    /// </summary>
    /// <returns></returns>
    private bool MediaIsPlaying => _entities.MediaPlayer.ShieldTv?.State == "playing";

    // /// <summary>
    // ///     Called when ever state change for the media_players playing on the TV
    // /// </summary>
    // private void OnMediaStateChanged(EntityState? to, EntityState? from)
    // {
    //     if (to?.State == "playing")
    //     {
    //         _timeStoppedPlaying = null;
    //         TurnOnTvIfOff(to.EntityId);
    //     }
    //     else
    //     {
    //         if (from?.State == "playing")
    //         {
    //             _timeStoppedPlaying = DateTime.Now;
    //             // Check in 20 minutes if TV is on and nothing still playing
    //             _scheduler.RunIn(_idleTimeout,
    //                 () =>
    //                 {
    //                     if (TvIsOn && !MediaIsPlaying && _timeStoppedPlaying != null)
    //                         if (DateTime.Now.Subtract(_timeStoppedPlaying.Value) >= _idleTimeout)
    //                             // Idle timeout went by without any change in state turn off TV
    //                             _log.LogInformation("TV been idle for {IdleTimeOut} minutes, turning off",
    //                                 _idleTimeout);
    //                     // If the state did has changed after we waited just run to completion
    //                 });
    //         }
    //     }
    // }

    // /// <summary>
    // ///     Turns the TV on if not on and pauses any playing media until TV is fully on
    // /// </summary>
    // private void TurnOnTvIfOff(string entityId)
    // {
    //     if (!TvIsOn && !_isTurningOnTv)
    //     {
    //         // Tv is of and there are not an operation turning it on
    //         _isTurningOnTv = true;
    //         _log.LogInformation("TV is not on, pause media {EntityId} and turn on tv!", entityId);
    //
    //         // Tv and light etc is managed through a RunScript
    //         _entities.Remote.Tvrummet.TurnOn("TV");
    //     }
    //
    //     if (!_isTurningOnTv) return;
    //
    //     _currentlyPausedMediaPlayer = new MediaPlayerEntity(_ha, entityId);
    //     _currentlyPausedMediaPlayer.MediaPause();
    // }

    // /// <summary>
    // ///     When TV is on and we have paused media, play it
    // /// </summary>
    // public async Task OnTVTurnedOn()
    // {
    //     if (_isTurningOnTv && _currentlyPausedMediaPlayer is not null)
    //     {
    //         // We had just turned on tv with this RunScript and have a media player paused
    //         // First delay and wait for the TV to get ready
    //         _log.LogDebug("TV is turning on.. Wait 9 seconds to complete...");
    //         _scheduler.Schedule(TimeSpan.FromSeconds(9), () =>
    //         {
    //             _isTurningOnTv = false;
    //             if (!MediaIsPlaying) _currentlyPausedMediaPlayer.MediaPlay();
    //         });
    //     }
    //
    //     await HandleOnTvOn().ConfigureAwait(false);
    // }

    private async Task HandleOnTvOn()
    {
        _entities.Light.TvrumBakgrundTv.TurnOn(0, xyColor: new[] {0.136, 0.04});
        await Task.Delay(200).ConfigureAwait(false);
        _entities.Light.TvrumVagg.TurnOff(0);
        await Task.Delay(200).ConfigureAwait(false);
        // _entities.Switch.JulbelysningTvrummet.TurnOff();
        _entities.Light.Tvrummet.TurnOff(0);
        await Task.Delay(200).ConfigureAwait(false);
        if (_entities.Cover.TvrumRullgardinHoger?.Attributes?.Position < 100)
            _entities.Cover.TvrumRullgardinHoger.CloseCover();
        await Task.Delay(200).ConfigureAwait(false);
        if (_entities.Cover.TvrumRullgardinVanster?.Attributes?.Position < 100)
            _entities.Cover.TvrumRullgardinVanster.CloseCover();
    }

    private async Task HandleOnTvOff()
    {
        _entities.Light.TvrumBakgrundTv.TurnOff(0);
        await Task.Delay(200).ConfigureAwait(false);
        _entities.MediaPlayer.ShieldTv.TurnOff();
        await Task.Delay(200).ConfigureAwait(false);
        if (IsNight || IsEvening)
            _entities.Light.Tvrummet.TurnOn(0);
    }
}
