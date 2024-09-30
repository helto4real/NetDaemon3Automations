using System.Reactive.Concurrency;

/// <summary>
///    Manage room presence 
/// </summary>
[NetDaemonApp]
public class RoomPresence
{
    private readonly string[] _homeStates = ["Hemma", "Nyss anlänt"];

    public RoomPresence(Services services, Entities entities, ILogger<RoomPresence> logger, IScheduler scheduler)
    {
        HandleBleTransmitterAvailabilityForTomasWatch();

        void HandleBleTransmitterAvailabilityForTomasWatch()
        {
            entities.Person.Tomas.StateChanges()
                .Subscribe(s =>
                {
                    if (_homeStates.Contains(s.New?.State) && !_homeStates.Contains(s.Old?.State))
                    {
                        logger.LogInformation("Tomas watch - BLE Transmitter is on");
                        services.Notify.MobileAppGalaxyWatch6ClassicJq4a("command_ble_transmitter", null, null, new { command = "turn_on" });
                    }
                    else if (!_homeStates.Contains(s.New?.State) && _homeStates.Contains(s.Old?.State))
                    {
                        logger.LogInformation("Tomas watch - BLE Transmitter is off");
                        services.Notify.MobileAppGalaxyWatch6ClassicJq4a("command_ble_transmitter", null, null, new { command = "turn_off" });
                    }
                });

            entities.Sensor.TomasKlockaBle.StateChanges()
                .WhenStateIsFor(e => e?.State == "sovrummet" && IsNight(), TimeSpan.FromMinutes(25), scheduler)
                .Subscribe(_ =>
                    {
                        logger.LogInformation("Tomas watch - BLE Transmitter is off due to Tomas is in bedroom and night mode");
                        services.Notify.MobileAppGalaxyWatch6ClassicJq4a("command_ble_transmitter", null, null, new { command = "turn_off" });
                    });

            scheduler.ScheduleCron($"0 7 * * *",
                () =>
                {
                    if (entities.Person.Tomas?.State == "Hemma")
                    {
                        logger.LogInformation("Tomas watch - BLE Transmitter is on due to morning time");
                        services.Notify.MobileAppGalaxyWatch6ClassicJq4a("command_ble_transmitter", null, null, new { command = "turn_on" });
                    }
                });

            entities.InputSelect.HouseModeSelect.StateChanges()
                .WhenStateIsFor(e => e?.State == "Natt" && TomasIsInBedroom(), TimeSpan.FromMinutes(25), scheduler)
                .Subscribe(s =>
                    {
                        logger.LogInformation("Tomas watch - BLE Transmitter is off due to night mode and Tomas is in bedroom");
                        services.Notify.MobileAppGalaxyWatch6ClassicJq4a("command_ble_transmitter", null, null, new { command = "turn_off" });
                    });
        }
        bool IsNight() => entities.InputSelect.HouseModeSelect?.State == "Natt";
        bool TomasIsInBedroom() => entities.Sensor.TomasKlockaBle?.State == "sovrummet";
    }
}
