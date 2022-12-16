using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NetDaemon.Extensions.Observables;

/// <summary>
///     Manage state of morning, house, day, evening, night and cleaning
/// </summary>
[NetDaemonApp]
// [Focus]
public class HouseStateManager
{
    private readonly IEntities _entities;
    private readonly ILogger<HouseStateManager> _log;
    private readonly GlobalConfig _gc;
    private readonly IScheduler _scheduler;

    public HouseStateManager(
        IEntities entities,
        IScheduler scheduler,
        ILogger<HouseStateManager> logger,
        IAppConfig<GlobalConfig> config)
    {
        _entities = entities;
        _scheduler = scheduler;
        _log = logger;
        _gc = config.Value;

        InitDayTimeSubscriptions();
        InitEveningTimeSubscriptions();
        InitNightTimeSubscriptions();
        InitMorningTimeSubscriptions();
        InitHouseStateSceneManagement();
    }

    private bool IsDaytime => _entities.InputSelect.HouseModeSelect.State == "Dag";
    private bool IsNighttime => _entities.InputSelect.HouseModeSelect.State == "Natt";

    /// <summary>
    ///     Sets the house state on the corresponding scene
    /// </summary>
    private void InitHouseStateSceneManagement()
    {
        _entities.Scene.Dag.WhenTurnsOn(s => SetHouseState(HouseState.Day));
        _entities.Scene.Kvall.WhenTurnsOn(s => SetHouseState(HouseState.Evening));
        _entities.Scene.Natt.WhenTurnsOn(s => SetHouseState(HouseState.Night));
        _entities.Scene.Morgon.WhenTurnsOn(s => SetHouseState(HouseState.Morning));
        _entities.Scene.Stadning.WhenTurnsOn(s => SetHouseState(HouseState.Cleaning));
    }

    private void InitDayTimeSubscriptions()
    {
        _log.LogInformation("Setting daytime: {DayTime}", _gc.DayTime);
        _scheduler.ScheduleCron($"{_gc.DayTime.Minutes} {_gc.DayTime.Hours} * * *", () => SetHouseState(HouseState.Day));
    }

    /// <summary>
    ///     Set night time schedule on different time different weekdays
    /// </summary>
    private void InitNightTimeSubscriptions()
    {
        _log.LogInformation("Setting weekday night time to: {NightTime}",
            _gc.NightTimeWeekdays);
        _scheduler.ScheduleCron($"{_gc.NightTimeWeekdays.Minutes} {_gc.NightTimeWeekdays.Hours} * * 0-4",
            () => SetHouseState(HouseState.Night));

        _log.LogInformation("Setting weekend night time to: {NightTime}",
            _gc.NightTimeWeekends);
        _scheduler.ScheduleCron($"{_gc.NightTimeWeekends.Minutes} {_gc.NightTimeWeekends.Hours} * * 5-6",
            () => SetHouseState(HouseState.Night));
    }

    /// <summary>
    ///     Set to evening when the light level is low and it is daytime
    /// </summary>
    private void InitEveningTimeSubscriptions()
    {
        _scheduler.ScheduleCron("15 15 * * *", () =>
        {
            if (_entities.Sensor.LightOutsideIlluminanceLux.State <= 20.0 && IsDaytime)
                SetHouseState(HouseState.Evening);
        });
        _entities.Sensor.LightOutsideIlluminanceLux
            .StateChanges()
            .WhenStateIsFor(n =>
                n?.State <= 20.0 &&
                _scheduler.Now.Hour is >= 14 and < 23 &&
                IsDaytime, TimeSpan.FromMinutes(15), _scheduler)

            .Subscribe(s => SetHouseState(HouseState.Evening));
    }

    /// <summary>
    ///     When the light levels are bright enough it is considered morning time
    ///     if the time frame is within time
    /// </summary>
    private void InitMorningTimeSubscriptions()
    {
        _scheduler.ScheduleCron("15 5 * * *", () =>
        {
            _log.LogInformation("It is 5:15 and light outside is {State}", _entities.Sensor.LightOutsideIlluminanceLux?.State);
            if (_entities.Sensor.LightOutsideIlluminanceLux?.State >= 35.0 && IsNighttime)
            {
                _log.LogInformation("It is 5:15 and setting morning house state");
                SetHouseState(HouseState.Morning);
            }
        });

        _entities.Sensor.LightOutsideIlluminanceLux
            .StateChanges()
            .WhenStateIsFor(n =>
                    n?.State >= 35.0 &&
                     IsNighttime &&
                    _scheduler.Now.LocalDateTime.Hour is >= 5 and < 10
                               , TimeSpan.FromMinutes(15)
                               , _scheduler)
            .Subscribe(_ => SetHouseState(HouseState.Morning));
        
        _entities.Sensor.LightOutsideIlluminanceLux
            .StateChanges()
            .WhenStateIsFor(n =>
                    n?.State >= 35.0
                , TimeSpan.FromMinutes(15)
                , _scheduler)
            .Subscribe(_ => _log.LogInformation("Now it is morningtime due to lightlevel {State} and hour {Hour}", _entities.Sensor.LightOutsideIlluminanceLux?.State, _scheduler.Now.Hour));
    }

    /// <summary>
    ///     Sets the house state to specified state and updates Home Assistant InputSelect
    /// </summary>
    /// <param name="state">State to set</param>
    private void SetHouseState(HouseState state)
    {
        _log.LogInformation("Setting current house state to {State}", state);
        var selectState = state switch
        {
            HouseState.Morning => "Morgon",
            HouseState.Day => "Dag",
            HouseState.Evening => "Kväll",
            HouseState.Night => "Natt",
            HouseState.Cleaning => "Städning",
            _ => throw new InvalidOperationException($"State {state} Not supported")
        };
        _entities.InputSelect.HouseModeSelect.SelectOption(selectState);
    }
}

public enum HouseState
{
    Morning,
    Day,
    Evening,
    Night,
    Cleaning,
    Unknown
}
