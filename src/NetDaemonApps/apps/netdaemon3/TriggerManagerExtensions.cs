public static class TriggerManagerExtensions
{
    public static IObservable<String?> RegisterMqttActionTrigger(this ITriggerManager triggerManager, object mqttDeviceName)
    {
        var triggerTopic = triggerManager.RegisterTrigger(new 
                {
                    platform = "mqtt",
                    topic = $"zigbee2mqtt/{mqttDeviceName}/action"
                });
        return triggerTopic.Select(e => e.GetProperty("payload").GetString());
    }
}
