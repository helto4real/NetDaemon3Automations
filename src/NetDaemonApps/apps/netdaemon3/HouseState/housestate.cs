using System.Reactive.Concurrency;

/// <summary>
///     Manage state of morning, house, day, evening, night and cleaning
/// </summary>
[NetDaemonApp]
// [Focus]
public class HouseStateManager
{
    private readonly Entities _entities;
    private readonly ILogger<HouseStateManager> _log;
    private readonly IScheduler _scheduler;

    public HouseStateManager(IHaContext ha, IScheduler scheduler, ILogger<HouseStateManager> logger)
    {
        _entities = new Entities(ha);
        _scheduler = scheduler;
        _log = logger;

        SetDayTime();
        SetEveningWhenLowLightLevel();
        SetNightTime();
        SetMorningWhenBrightLightLevel();
        InitHouseStateSceneManagement();
    }

    public bool IsDaytime => _entities.InputSelect.HouseModeSelect.State == "Dag";
    public bool IsNighttime => _entities.InputSelect.HouseModeSelect.State == "Natt";


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

    private void SetDayTime()
    {
        _log.LogInformation($"Setting daytime: 09:00:00");
        _scheduler.ScheduleCron("0 9 * * *", () => SetHouseState(HouseState.Day));
    }

    /// <summary>
    ///     Set night time schedule on different time different weekdays
    /// </summary>
    private void SetNightTime()
    {
        _log.LogInformation($"Setting weekday night time to: 22:40");
        _scheduler.ScheduleCron("40 22 * * 0-4", () => SetHouseState(HouseState.Night));

        _log.LogInformation($"Setting weekend night time to: 23:40");
        _scheduler.ScheduleCron("40 23 * * 5-6", () => SetHouseState(HouseState.Night));
    }

    /// <summary>
    ///     Set to evening when the light level is low and it is daytime
    /// </summary>
    private void SetEveningWhenLowLightLevel()
    {
        _entities.Sensor.LightOutside
            .StateChanges()
            .Where(e => _entities.Sensor.LightOutside.AsNumeric().State <= 20.0 &&
                        _scheduler.Now.Hour is >= 15 and < 23 && IsDaytime)
            .Subscribe(s => SetHouseState(HouseState.Evening));
    }

    /// <summary>
    ///     When the light levels are bright enough it is considered morning time
    /// </summary>
    private void SetMorningWhenBrightLightLevel()
    {
        _entities.Sensor.LightOutside
            .StateChanges()
            .Where(e => _entities.Sensor.LightOutside.AsNumeric().State >= 35.0 &&
                        _scheduler.Now.Hour is >= 5 and < 10 && IsNighttime
            )
            .Subscribe(_ => SetHouseState(HouseState.Morning));
    }

    /// <summary>
    ///     Sets the house state to specified state and updates Home Assistant InputSelect
    /// </summary>
    /// <param name="state">State to set</param>
    private void SetHouseState(HouseState state)
    {
        _log.LogInformation($"Setting current house state to {state}", state);
        var select_state = state switch
        {
            HouseState.Morning => "Morgon",
            HouseState.Day => "Dag",
            HouseState.Evening => "Kväll",
            HouseState.Night => "Natt",
            HouseState.Cleaning => "Städning",
            _ => throw new InvalidOperationException($"State {state} Not supported")
        };
        _entities.InputSelect.HouseModeSelect.SelectOption(select_state);
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
