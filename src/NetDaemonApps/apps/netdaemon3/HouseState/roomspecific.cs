using System.Reactive.Concurrency;

/// <summary>
///     Room specific automations.
/// </summary>
[NetDaemonApp]
public class RoomSpecificManager
{
    private readonly BinarySensorEntities _binarySensors;
    private readonly LightEntities _lights;
    private readonly IScheduler _scheduler;
    private readonly SwitchEntities _switches;
    private readonly DeviceTrackerEntities _trackers;

    public RoomSpecificManager(IHaContext ctx, LightEntities lights, SwitchEntities switches,
        DeviceTrackerEntities trackers, BinarySensorEntities binarySensors, IScheduler scheduler)
    {
        _lights = lights;
        _switches = switches;
        _trackers = trackers;
        _binarySensors = binarySensors;
        _scheduler = scheduler;
        // SetupTomasComputerAutoStart();
        SetupTurnOffKidsLightsEarly();
    }

    private bool TomasIsHome => _trackers.TomasPresence.State == "Hemma";

    private void SetupTurnOffKidsLightsEarly()
    {
        _scheduler.ScheduleCron("0 22 * * *", TurnOffKidsLight);
    }

    private void TurnOffKidsLight()
    {
        var kidsRoomLights = new Entity[]
        {
            _lights.MelkersRum,
            _lights.SallysRum
        };
        kidsRoomLights.CallService("turn_off", new { transition = 0 });
    }

    private void SetupTomasComputerAutoStart()
    {
        // Turn on computer if Tomas is home and enter room
        _binarySensors.TomasRumPirOccupancy
            .StateChanges()
            .Where(e =>
                e.New.IsOn() &&
                _switches.ComputerTomas.IsOff() &&
                TomasIsHome)
            .Subscribe(
                _ => _switches.ComputerTomas.TurnOn());

        // Turn off computer if no movement for one hour en Tomas room
        _binarySensors.TomasRumPirOccupancy
            .StateChanges()
            .WhenStateIsFor(e => e.IsOff(), TimeSpan.FromHours(1), _scheduler)
            .Subscribe(_ => _switches.ComputerTomas.TurnOff());
    }
}
