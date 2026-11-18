using System.Reactive.Concurrency;

namespace NetDaemonApps;

public sealed class TvConfig
{
    public TimeSpan RoomVacancyTimeout { get; init; } = TimeSpan.FromMinutes(20);

    public TimeSpan TelevisionOffDebounce { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan CommandDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public IReadOnlyList<double> BackgroundLightXyColor { get; init; } = [0.136, 0.04];
}

[NetDaemonApp]
public sealed class TvManager : ReactiveApp, IAsyncInitializable
{
    private readonly Entities _entities;
    private readonly ILogger<TvManager> _logger;
    private readonly IScheduler _scheduler;
    private readonly ICommandSequencer _commandSequencer;
    private readonly TvConfig _config;
    private IDisposable? _turnOffLightSchedule;
    private CancellationToken _cancellationToken;

    public TvManager(
        Entities entities,
        ILogger<TvManager> logger,
        IScheduler scheduler,
        ICommandSequencer commandSequencer,
        IAppConfig<TvConfig> config)
    {
        _entities = entities;
        _logger = logger;
        _scheduler = scheduler;
        _commandSequencer = commandSequencer;
        _config = config.Value;

        ConfigurationValidation.Positive(_config.RoomVacancyTimeout, nameof(TvConfig.RoomVacancyTimeout));
        ConfigurationValidation.NonNegative(
            _config.TelevisionOffDebounce,
            nameof(TvConfig.TelevisionOffDebounce));
        ConfigurationValidation.NonNegative(_config.CommandDelay, nameof(TvConfig.CommandDelay));
        ArgumentNullException.ThrowIfNull(_config.BackgroundLightXyColor);
        if (_config.BackgroundLightXyColor.Count != 2 ||
            _config.BackgroundLightXyColor.Any(coordinate => !double.IsFinite(coordinate) || coordinate is < 0 or > 1))
        {
            throw new InvalidOperationException(
                "Background light XY color must contain two finite coordinates between zero and one.");
        }
    }

    private bool IsNight => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Night;

    private bool IsEvening => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Evening;

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        _cancellationToken = cancellationToken;

        SubscribeLogged(
            _entities.MediaPlayer.SamsungQ60Series65.StateChanges()
                .WhenStateIsFor(state => state.IsOff(), _config.TelevisionOffDebounce, _scheduler),
            _logger,
            "turn off TV remote after television powers down",
            _ => _entities.Remote.Tvrummet.TurnOff());

        SubscribeLoggedAsync(
            _entities.Remote.Tvrummet.StateAllChanges()
                .Where(change => change.New?.Attributes?.CurrentActivity != change.Old?.Attributes?.CurrentActivity),
            _logger,
            "handle TV activity change",
            HandleActivityChangedAsync);

        SubscribeLogged(
            _entities.BinarySensor.TvrummEprlOccupancy.StateChanges()
                .WhenStateIsFor(state => state.IsOff(), _config.RoomVacancyTimeout, _scheduler),
            _logger,
            "turn off TV after room vacancy",
            _ =>
            {
                _logger.LogInformation("TV room has been vacant; turning off the TV");
                _entities.Remote.Tvrummet.TurnOff();
            });

    }, cancellationToken);

    private async Task HandleActivityChangedAsync(StateChange<RemoteEntity, EntityState<RemoteAttributes>> change)
    {
        var activity = change.New?.Attributes?.CurrentActivity;
        _logger.LogDebug(
            "TV remote activity changed from {PreviousActivity} to {CurrentActivity}",
            change.Old?.Attributes?.CurrentActivity,
            activity);

        switch (activity)
        {
            case "TV":
            case "Film":
                await HandleTvOnAsync().ConfigureAwait(false);
                break;
            case "PowerOff":
                await HandleTvOffAsync().ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleTvOnAsync()
    {
        ResetTurnOffLightSchedule();

        var actions = new Action[]
        {
            () => _entities.Light.TvrumBakgrundTv.TurnOn(
                transition: 0,
                xyColor: _config.BackgroundLightXyColor),
            () =>
            {
                if (_entities.Cover.TvrumRullgardinHoger.Attributes?.CurrentPosition < 100)
                {
                    _entities.Cover.TvrumRullgardinHoger.CloseCover();
                }
            },
            () =>
            {
                if (_entities.Cover.TvrumRullgardinVanster.Attributes?.CurrentPosition < 100)
                {
                    _entities.Cover.TvrumRullgardinVanster.CloseCover();
                }
            },
        };

        await ExecuteSequenceAsync(actions, "prepare TV room").ConfigureAwait(false);
    }

    private async Task HandleTvOffAsync()
    {
        var actions = new Action[]
        {
            () => _entities.Light.TvrumBakgrundTv.TurnOff(0),
            () => _entities.MediaPlayer.ShieldTv.TurnOff(),
        };

        await ExecuteSequenceAsync(actions, "shut down TV room").ConfigureAwait(false);

        if (!_entities.Light.Tvrummet.IsOn())
        {
            return;
        }

        if (IsEvening)
        {
            _entities.Light.Tvrummet.TurnOn(0);
        }
        else if (IsNight)
        {
            _entities.Light.TvrumVagg.TurnOn(0);
            ResetTurnOffLightSchedule();
            _turnOffLightSchedule = Track(_scheduler.Schedule(_config.RoomVacancyTimeout, () =>
            {
                if (IsNight)
                {
                    _entities.Light.Tvrummet.TurnOff(0);
                }
            }));
        }
    }

    private Task ExecuteSequenceAsync(IEnumerable<Action> actions, string operation) => ExecuteLoggedAsync(
        _commandSequencer,
        actions,
        _config.CommandDelay,
        _logger,
        operation,
        _cancellationToken);

    private void ResetTurnOffLightSchedule()
    {
        _turnOffLightSchedule?.Dispose();
        _turnOffLightSchedule = null;
    }
}
