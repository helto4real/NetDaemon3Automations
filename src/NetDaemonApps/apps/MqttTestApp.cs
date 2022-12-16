// using System.Threading;
// using System.Threading.Tasks;
// using NetDaemon.Extensions.MqttEntityManager;
//
// namespace HomeAssistantGenerated.apps;

// [NetDaemonApp]
// public class TestApp
// {
//     public TestApp(IMqttEntityManager mqttEntityManager)
//     {
//     }
// }

// using System.Threading;
// using System.Threading.Tasks;
// using NetDaemon.Extensions.MqttEntityManager;
//
// namespace HomeAssistantGenerated.apps;
//
//
// using NetDaemon.Extensions.Persistance;
//
// public record CounterState
// {
//     public int Counter { get; set; }
// }
//
// [NetDaemonApp]
// [Focus]
// public class PersistanceApp
// {
//     public PersistanceApp(IPersistState<CounterState> counterState, ILogger<PersistanceApp> logger)
//     {
//         logger.LogInformation("Current count is: {Count}", counterState.Value.Counter);
//         counterState.Value.Counter++;
//     }
//
// }
