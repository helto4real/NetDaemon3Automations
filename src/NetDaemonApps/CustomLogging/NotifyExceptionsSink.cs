using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;
using NetDaemon.Client;
using NetDaemon.Client.HomeAssistant.Extensions;
using Serilog.Core;
using Serilog.Events;

class NotifyExceptionsSink : ILogEventSink
{
    private readonly Subject<string> _subject = new();

    public NotifyExceptionsSink(IServiceProvider provider)
    {
        _subject
            .Throttle(TimeSpan.FromSeconds(2))
            .Subscribe(async message =>
            {
                var connection = provider.GetService<IHomeAssistantConnection>();
                var notification = string.Concat("👿 Exception in NetDaemon 👿: \r\n", message);
                await connection?.CallServiceAsync("notify", "hass_notifications", new { message = notification, target="1264853019489599539" });
            });
    }
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        // prevent recursive logging
        if (message.Contains("Exception in NetDaemon")) return;
        _subject.OnNext(message);
    }
}
