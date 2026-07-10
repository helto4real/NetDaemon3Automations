namespace NetDaemonApps;

public sealed class EventStreamWatchdogConfig
{
    public TimeSpan EventTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromMinutes(1);
}

[NetDaemonApp]
public sealed class EventStreamWatchdog : ReactiveApp, IAsyncInitializable
{
    private readonly IHaContext _haContext;
    private readonly INetDaemonScheduler _scheduler;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EventStreamWatchdog> _logger;
    private readonly EventStreamWatchdogConfig _config;
    private DateTimeOffset _lastEvent;
    private bool _alerted;

    public EventStreamWatchdog(
        IHaContext haContext,
        INetDaemonScheduler scheduler,
        TimeProvider timeProvider,
        ILogger<EventStreamWatchdog> logger,
        IAppConfig<EventStreamWatchdogConfig> config)
    {
        _haContext = haContext;
        _scheduler = scheduler;
        _timeProvider = timeProvider;
        _logger = logger;
        _config = config.Value;
        ConfigurationValidation.Positive(_config.EventTimeout, nameof(EventStreamWatchdogConfig.EventTimeout));
        ConfigurationValidation.Positive(_config.CheckInterval, nameof(EventStreamWatchdogConfig.CheckInterval));
        _lastEvent = timeProvider.GetUtcNow();
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        SubscribeLogged(
            _haContext.StateAllChanges(),
            _logger,
            "monitor Home Assistant event stream",
            _ =>
            {
                _lastEvent = _timeProvider.GetUtcNow();
                _alerted = false;
            });

        Track(_scheduler.RunEvery(_config.CheckInterval, CheckEventStream));
    }, cancellationToken);

    private void CheckEventStream()
    {
        if (_alerted || _timeProvider.GetUtcNow() - _lastEvent < _config.EventTimeout)
        {
            return;
        }

        _alerted = true;
        _logger.LogWarning("No Home Assistant state events received for {EventTimeout}", _config.EventTimeout);
        _haContext.CallService(
            "persistent_notification",
            "create",
            data: new
            {
                message = "Slutade att få events",
                title = "NetDaemon får inte längre Home Assistant-events",
            });
    }
}
