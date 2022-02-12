using NetDaemon.Extensions.Persistance;

public class CarHeaterConfig
{
    /// <summary>
    ///     Sensor used for temperature outside, from config
    /// </summary>
    public SensorEntity? TempSensor { get; set; }

    /// <summary>
    ///     Sensor that has departure time HH:mm, from config
    /// </summary>
    public SensorEntity? DepartureTimeSensor { get; set; }

    /// <summary>
    ///     Is true if departure time is valid on weekend, from config
    /// </summary>
    public InputBooleanEntity? ScheduleOneWeekendsInputBoolean { get; set; }

    /// <summary>
    ///     Switch used for turning on heater
    /// </summary>
    public SwitchEntity? HeaterSwitch { get; set; }
}

/// <summary>
///     Application manage the carheater. Implements following use-cases:
///     - Automatically turns off carheater if on for 3 hours as protection for the car
///     - Reads departure time and turn on heater a specific time
///     before depending on temperature
///     - Can be turned on/off depending if it is a weekday or weekend
///     The application is running every minute and decides if the heater is
///     going to be on or off. This logic will work also after restart.
/// </summary>
//[NetDaemonApp]
public class CarHeaterManager
{
    private readonly CarHeaterConfig _config;

    private readonly Entities _entities;
    private readonly ILogger<CarHeaterManager> _log;
    private readonly INetDaemonScheduler _scheduler;

    private readonly Services _services;
    private readonly IStateRepository _storage;

    // True if the script just turn on the heater,
    // used to prohibit logic being run on state change
    private bool _appChangedState;

    // Used for logging at startup and no more
    private bool _appJustStarted = true;

    public CarHeaterManager(IHaContext ha, INetDaemonScheduler scheduler, ILogger<CarHeaterManager> logger,
        IStateRepository storage, IAppConfig<CarHeaterConfig> config)
    {
        _entities = new Entities(ha);
        _services = new Services(ha);
        _scheduler = scheduler;
        _log = logger;
        _storage = storage;
        _config = config.Value;
        Initialize();
    }

    private bool IsManualState => (_storage.GetState<string>("Car_IsManualState") ?? "false") == "true";

    /// <summary>
    ///     Initialize the automations
    /// </summary>
    /// <remarks>
    ///     - Schedules check every minute if heater should be on or off depending on temperature
    ///     - Set the manually started flag if heater is turn on and not turned on by this script
    /// </remarks>
    public void Initialize()
    {
        // Get the state if manually started from statestorage
        _scheduler.RunEvery(TimeSpan.FromMinutes(1), () => HandleCarHeater());

        _config.HeaterSwitch?
            .WhenTurnsOn(s =>
            {
                if (_appChangedState == false)
                    // It is manually turned on
                    _storage.SetState<string>("Car_IsManualState", "true");
                else
                    _storage.SetState<string>("Car_IsManualState", "false");
                _appChangedState = false;
            });
    }

    /// <summary>
    ///     Handle the logic run every minute if heater
    ///     should be on or off
    /// </summary>
    private void HandleCarHeater()
    {
        try
        {
            // First do the failsave logic, no heater should run for more than 3 hours
            TurnOffHeaterIfOnMoreThanThreeHours();

            // Get relevant states
            var currentOutsideTemp = _config.TempSensor?.AsNumeric().State;
            var configuredDepartureTime = _config.DepartureTimeSensor?.State;
            var scheduleOnWeekend = _config.ScheduleOneWeekendsInputBoolean.IsOn();

            // Calculate correct set departure time
            var now = DateTime.Now;
            var hours = int.Parse(configuredDepartureTime!.Split(':')[0]); // configured departure is in format hh:mm
            var minutes = int.Parse(configuredDepartureTime.Split(':')[1]);
            var nextDeparture = new DateTime(now.Year, now.Month, now.Day, hours, minutes, 0);

            // Add the next day if we passed todays time
            if (nextDeparture < now)
                nextDeparture = nextDeparture.AddDays(1);

            if (_appJustStarted)
            {
                // Just log some useful information if we at startup
                _log.LogInformation("The time is {time}, if the time does not match local time, see time zone settings",
                    DateTime.Now);
                _log.LogInformation("Next departure is {nextDeparture}", nextDeparture);
                _appJustStarted = false;
            }


            // If weekend and not set to schedule on weekends then just return
            if ((nextDeparture.DayOfWeek == DayOfWeek.Saturday || nextDeparture.DayOfWeek == DayOfWeek.Sunday) &&
                !scheduleOnWeekend) return;

            // Calculate total minutes to departure
            var totalMinutesUntilDeparture = nextDeparture.Subtract(now).TotalMinutes;

            if (currentOutsideTemp >= -1.0 && currentOutsideTemp <= 5.0)
            {
                // Within 30 minutes
                if (totalMinutesUntilDeparture <= 30)
                {
                    TurnOnHeater();
                    return;
                }
            }
            else if (currentOutsideTemp >= -5.0 && currentOutsideTemp < -1.0)
            {
                // Within one hour
                if (totalMinutesUntilDeparture <= 60)
                {
                    TurnOnHeater();
                    return;
                }
            }
            else if (currentOutsideTemp >= -10.0 && currentOutsideTemp < -5.0)
            {
                // Within 1.5 hour
                if (totalMinutesUntilDeparture <= 90)
                {
                    TurnOnHeater();
                    return;
                }
            }
            else if (currentOutsideTemp >= -20.0 && currentOutsideTemp < -10.0)
            {
                // Within two hours
                if (totalMinutesUntilDeparture <= 120)
                {
                    TurnOnHeater();
                    return;
                }
            }
            else if (currentOutsideTemp < -20.0)
            {
                // Within three hours
                if (totalMinutesUntilDeparture <= 180)
                {
                    TurnOnHeater();
                    return;
                }
            }

            // If not manually started and heater is on, turn heater off
            if (_config.HeaterSwitch.IsOn() && !IsManualState)
            {
                _log.LogInformation("Turning off heater");
                _appChangedState = true;
                _config.HeaterSwitch?.TurnOff();
                _log.LogInformation("Next departure is {nextDeparture}", nextDeparture);
            }
        }
        catch (Exception e)
        {
            // Log all errors!
            _log.LogError(e, "Error in car heater app");
        }
    }

    /// <summary>
    ///     Turn the heater on if it is not already on
    /// </summary>
    private void TurnOnHeater()
    {
        try
        {
            // Temp is debuginformation to make sure the logic works, will be removed
            var currentOutsideTemp = _config.TempSensor?.AsNumeric().State;

            if (_config.HeaterSwitch.IsOn())
            {
                // Flag that this script actually turn the heater on and non manually
                _appChangedState = true;

                _log.LogInformation("{time} : Turn on heater temp ({temp})", DateTime.Now,
                    currentOutsideTemp.ToString() ?? "unavailable");
                _config.HeaterSwitch?.TurnOn();
            }
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error turn on heater");
        }
    }

    /// <summary>
    ///     Turns the heater off if it has been on for more than three hours
    /// </summary>
    /// <remarks>
    ///     For any reason the switch has been on for more than three hours
    ///     the heater will be turned off. This will save energy and prohibit
    ///     the heater being on accidentally
    /// </remarks>
    /// <returns></returns>
    private void TurnOffHeaterIfOnMoreThanThreeHours()
    {
        try
        {
            if (_config.HeaterSwitch.IsOn())
                if (DateTime.Now.Subtract(_config.HeaterSwitch?.EntityState?.LastChanged ?? DateTime.MinValue) >
                    TimeSpan.FromHours(3))
                    _config.HeaterSwitch?.TurnOff();
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error doing failsafe");
        }
    }

    // The entities used in the automation
    // private readonly string _heaterSwitch = "switch.motorvarmare";

    #region -- config properties --

    #endregion
}