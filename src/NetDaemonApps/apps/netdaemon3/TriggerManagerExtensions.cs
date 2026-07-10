namespace NetDaemonApps;

public static class TriggerManagerExtensions
{
    public static IObservable<string> RegisterMqttActionTrigger(
        this ITriggerManager triggerManager,
        string mqttDeviceName)
    {
        ArgumentNullException.ThrowIfNull(triggerManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(mqttDeviceName);

        var triggerTopic = triggerManager.RegisterTrigger(new
        {
            platform = "mqtt",
            topic = $"zigbee2mqtt/{mqttDeviceName}/action",
        });

        return triggerTopic
            .Select(element => element.TryGetProperty("payload", out var payload) ? payload.GetString() : null)
            .Where(payload => !string.IsNullOrWhiteSpace(payload))
            .Select(payload => payload!);
    }
}
