using System.Reactive.Concurrency;

namespace NetDaemonApps;

[NetDaemonApp]
public sealed class HouseStateManager : ReactiveApp, IAsyncInitializable
{
    private readonly Entities _entities;
    private readonly ILogger<HouseStateManager> _logger;
    private readonly GlobalConfig _config;
    private readonly IScheduler _scheduler;

    public HouseStateManager(
        Entities entities,
        IScheduler scheduler,
        ILogger<HouseStateManager> logger,
        IAppConfig<GlobalConfig> config)
    {
        _entities = entities;
        _scheduler = scheduler;
        _logger = logger;
        _config = config.Value;

        ConfigurationValidation.DailyTime(_config.MorningTime, nameof(GlobalConfig.MorningTime));
        ConfigurationValidation.DailyTime(_config.DayTime, nameof(GlobalConfig.DayTime));
        ConfigurationValidation.DailyTime(_config.EveningCheckTime, nameof(GlobalConfig.EveningCheckTime));
        ConfigurationValidation.DailyTime(_config.NightTimeWeekdays, nameof(GlobalConfig.NightTimeWeekdays));
        ConfigurationValidation.DailyTime(_config.NightTimeWeekends, nameof(GlobalConfig.NightTimeWeekends));
        ConfigurationValidation.Positive(
            _config.IlluminanceStabilityDuration,
            nameof(GlobalConfig.IlluminanceStabilityDuration));
        ConfigurationValidation.Finite(
            _config.EveningIlluminanceThreshold,
            nameof(GlobalConfig.EveningIlluminanceThreshold));
        ConfigurationValidation.Finite(
            _config.MorningIlluminanceThreshold,
            nameof(GlobalConfig.MorningIlluminanceThreshold));
    }

    private bool IsDaytime => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Day;

    private bool IsNighttime => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Night;

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        InitializeDaySchedule();
        InitializeEveningRules();
        InitializeNightSchedule();
        InitializeMorningRules();
        InitializeSceneRules();
    }, cancellationToken);

    private void InitializeSceneRules()
    {
        Track(_entities.Scene.Dag.WhenTurnsOn(_ => SetHouseMode(HouseMode.Day)));
        Track(_entities.Scene.Kvall.WhenTurnsOn(_ => SetHouseMode(HouseMode.Evening)));
        Track(_entities.Scene.Natt.WhenTurnsOn(_ => SetHouseMode(HouseMode.Night)));
        Track(_entities.Scene.Morgon.WhenTurnsOn(_ => SetHouseMode(HouseMode.Morning)));
        Track(_entities.Scene.Stadning.WhenTurnsOn(_ => SetHouseMode(HouseMode.Cleaning)));
    }

    private void InitializeDaySchedule()
    {
        _logger.LogInformation("Setting daytime schedule to {DayTime}", _config.DayTime);
        Track(_scheduler.ScheduleCron(ToDailyCron(_config.DayTime), () => SetHouseMode(HouseMode.Day)));
    }

    private void InitializeNightSchedule()
    {
        _logger.LogInformation("Setting weekday night schedule to {NightTime}", _config.NightTimeWeekdays);
        Track(_scheduler.ScheduleCron(
            $"{_config.NightTimeWeekdays.Minutes} {_config.NightTimeWeekdays.Hours} * * 0-4",
            () => SetHouseMode(HouseMode.Night)));

        _logger.LogInformation("Setting weekend night schedule to {NightTime}", _config.NightTimeWeekends);
        Track(_scheduler.ScheduleCron(
            $"{_config.NightTimeWeekends.Minutes} {_config.NightTimeWeekends.Hours} * * 5-6",
            () => SetHouseMode(HouseMode.Night)));
    }

    private void InitializeEveningRules()
    {
        Track(_scheduler.ScheduleCron(ToDailyCron(_config.EveningCheckTime), () =>
        {
            if (_entities.Sensor.LightOutsideIlluminance.State <= _config.EveningIlluminanceThreshold && IsDaytime)
            {
                SetHouseMode(HouseMode.Evening);
            }
        }));

        SubscribeLogged(
            _entities.Sensor.LightOutsideIlluminance
                .StateChanges()
                .WhenStateIsFor(
                    state => state?.State <= _config.EveningIlluminanceThreshold &&
                             _scheduler.Now.Hour is >= 14 and < 23 &&
                             IsDaytime,
                    _config.IlluminanceStabilityDuration,
                    _scheduler),
            _logger,
            "set evening mode from illuminance",
            _ => SetHouseMode(HouseMode.Evening));
    }

    private void InitializeMorningRules()
    {
        Track(_scheduler.ScheduleCron(ToDailyCron(_config.MorningTime), () =>
        {
            if (_entities.Sensor.LightOutsideIlluminance.State >= _config.MorningIlluminanceThreshold && IsNighttime)
            {
                SetHouseMode(HouseMode.Morning);
            }
        }));

        SubscribeLogged(
            _entities.Sensor.LightOutsideIlluminance
                .StateChanges()
                .WhenStateIsFor(
                    state => state?.State >= _config.MorningIlluminanceThreshold &&
                             IsNighttime &&
                             _scheduler.Now.LocalDateTime.Hour is >= 5 and < 10,
                    _config.IlluminanceStabilityDuration,
                    _scheduler),
            _logger,
            "set morning mode from illuminance",
            _ => SetHouseMode(HouseMode.Morning));
    }

    private void SetHouseMode(HouseMode mode)
    {
        _logger.LogInformation("Setting current house mode to {HouseMode}", mode);
        _entities.InputSelect.HouseModeSelect.SelectOption(HouseModeCodec.Format(mode));
    }

    private static string ToDailyCron(TimeSpan time) => $"{time.Minutes} {time.Hours} * * *";
}
