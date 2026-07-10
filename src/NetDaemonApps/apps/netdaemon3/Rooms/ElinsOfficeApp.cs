namespace NetDaemonApps;

public sealed class ElinsOfficeConfig
{
    public string BlindsMqttDevice { get; init; } = "elins_rum_knapp_rullgardin";
}

[NetDaemonApp]
public sealed class ElinsOfficeApp : ReactiveApp, IAsyncInitializable
{
    private readonly Entities _entities;
    private readonly ITriggerManager _triggerManager;
    private readonly ILogger<ElinsOfficeApp> _logger;
    private readonly ElinsOfficeConfig _config;

    public ElinsOfficeApp(
        Entities entities,
        ITriggerManager triggerManager,
        ILogger<ElinsOfficeApp> logger,
        IAppConfig<ElinsOfficeConfig> config)
    {
        _entities = entities;
        _triggerManager = triggerManager;
        _logger = logger;
        _config = config.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.BlindsMqttDevice);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        SubscribeLogged(
            _triggerManager.RegisterMqttActionTrigger(_config.BlindsMqttDevice),
            _logger,
            "control Elin office blinds",
            action =>
            {
                switch (action)
                {
                    case HomeAssistantStates.Open:
                        _entities.Cover.ElinsRumRullgardin.OpenCover();
                        break;
                    case HomeAssistantStates.Close:
                        _entities.Cover.ElinsRumRullgardin.CloseCover();
                        break;
                }
            });

    }, cancellationToken);
}
