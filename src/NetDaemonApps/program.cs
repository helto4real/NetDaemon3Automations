using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
using NetDaemonApps;

try
{
    await Host.CreateDefaultBuilder(args)
        .UseNetDaemonAppSettings()
        .UseNetDaemonDefaultLogging()
        .UseNetDaemonRuntime()
        .UseNetDaemonTextToSpeech()
        .UseNetDaemonMqttEntityManagement()
        .ConfigureServices((_, services) =>
            services
                .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
                .AddNetDaemonStateManager()
                .AddNetDaemonScheduler()
                .AddSingleton(TimeProvider.System)
                .AddSingleton<ICommandSequencer, CommandSequencer>()
                .AddHomeAssistantGenerated()
        )
        .Build()
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to start host... {e}");
    throw;
}
