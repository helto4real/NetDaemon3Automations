using System.Reactive.Concurrency;

namespace NetDaemonApps;

[NetDaemonApp]
public sealed class RoomPresence : ReactiveApp, IAsyncInitializable
{
    private static readonly HashSet<string> HomeStates =
        [PresenceStates.Home, PresenceStates.JustArrived];

    private readonly Services _services;
    private readonly Entities _entities;
    private readonly ILogger<RoomPresence> _logger;
    private readonly IScheduler _scheduler;

    public RoomPresence(
        Services services,
        Entities entities,
        ILogger<RoomPresence> logger,
        IScheduler scheduler)
    {
        _services = services;
        _entities = entities;
        _logger = logger;
        _scheduler = scheduler;
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
    {
        SubscribeLogged(
            _entities.Person.Tomas.StateChanges(),
            _logger,
            "update watch BLE transmitter from presence",
            change =>
            {
                var isHome = HomeStates.Contains(change.New?.State ?? string.Empty);
                var wasHome = HomeStates.Contains(change.Old?.State ?? string.Empty);

                if (isHome != wasHome)
                {
                    SetBleTransmitter(isHome, "presence changed");
                }
            });

        SubscribeLogged(
            _entities.Sensor.TomasKlockaBle.StateChanges()
                .WhenStateIsFor(
                    state => state?.State == PresenceStates.Bedroom && IsNight,
                    TimeSpan.FromMinutes(25),
                    _scheduler),
            _logger,
            "disable watch BLE transmitter from bedroom presence",
            _ => SetBleTransmitter(enabled: false, "Tomas is in the bedroom during night mode"));

        SubscribeLogged(
            _entities.InputSelect.HouseModeSelect.StateChanges()
                .WhenStateIsFor(
                    state => state?.State == HouseModeCodec.Night && TomasIsInBedroom,
                    TimeSpan.FromMinutes(25),
                    _scheduler),
            _logger,
            "disable watch BLE transmitter from night mode",
            _ => SetBleTransmitter(enabled: false, "night mode started while Tomas is in the bedroom"));

        Track(_scheduler.ScheduleCron("0 7 * * *", () =>
        {
            if (_entities.Person.Tomas.State == PresenceStates.Home)
            {
                SetBleTransmitter(enabled: true, "morning schedule");
            }
        }));

    }, cancellationToken);

    private bool IsNight => _entities.InputSelect.HouseModeSelect.State == HouseModeCodec.Night;

    private bool TomasIsInBedroom => _entities.Sensor.TomasKlockaBle.State == PresenceStates.Bedroom;

    private void SetBleTransmitter(bool enabled, string reason)
    {
        var command = enabled ? "turn_on" : "turn_off";
        _logger.LogInformation("Tomas watch BLE transmitter is {Command} because {Reason}", command, reason);
        _services.Notify.MobileAppGalaxyWatch6ClassicJq4a(
            "command_ble_transmitter",
            data: new { command });
    }
}
