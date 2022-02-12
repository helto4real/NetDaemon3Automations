using System.Reactive.Concurrency;

/// <summary>
///     Room specific automations.
/// </summary>
[NetDaemonApp]
public class RoomSpecificManager
{
    private readonly IEntities _entities;
    private readonly IScheduler _scheduler;

    public RoomSpecificManager(IEntities entities, IScheduler scheduler)
    {
        _entities = entities;
        _scheduler = scheduler;

        SetupTomasComputerAutoStart();
        SetupManageMelkersChromecast();
        SetupTurnOffKidsLightsEarly();
    }

    public bool TomasIsHome => _entities.DeviceTracker.TomasPresence.State == "Hemma";

    private void SetupTurnOffKidsLightsEarly()
    {
        _scheduler.ScheduleCron("0 22 * * *", TurnOffKidsLight);
    }

    private void TurnOffKidsLight()
    {
        var kidsRoomLights = new Entity[]
        {
            _entities.Light.MelkersRum,
            _entities.Light.SallysRum
        };
        kidsRoomLights.CallService("turn_off", new {transition = 0});
    }

    private void SetupManageMelkersChromecast()
    {
        _scheduler.ScheduleCron("30 1 * * *", () =>
        {
            // Every night reset Melkers chromecast so the TV will auto shut off
            _entities.Switch.Switch8MelkersTv.TurnOff();
            _scheduler.Schedule(TimeSpan.FromMinutes(5), () => _entities.Switch.Switch8MelkersTv.TurnOn());
        });
    }

    private void SetupTomasComputerAutoStart()
    {
        // Turn on computer if Tomas is home and enter room
        _entities.BinarySensor.TomasRumPir
            .StateChanges()
            .Where(e =>
                e.New.IsOn() &&
                _entities.Switch.ComputerTomas.IsOff() &&
                TomasIsHome)
            .Subscribe(
                s => _entities.Switch.ComputerTomas.TurnOn());

        // Turn off computer if no movement for one hour en Tomas room
        _entities.BinarySensor.TomasRumPir
            .StateChanges()
            .Where(e => e.New.IsOff())
            .Throttle(TimeSpan.FromHours(1))
            .Subscribe(s => _entities.Switch.ComputerTomas.TurnOff());
    }
}
