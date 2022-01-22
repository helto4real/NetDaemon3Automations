using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using NetDaemon.Extensions.Scheduler;
using NSubstitute;

namespace NetDaemonApps.Tests.Helpers;

public class NetDaemonSchedulerMock
{
    public ILogger<NetDaemonScheduler> LoggerMock = Substitute.For<ILogger<NetDaemonScheduler>>();

    public NetDaemonSchedulerMock()
    {
        Scheduler = new NetDaemonScheduler(LoggerMock, TestScheduler);
    }

    public INetDaemonScheduler Scheduler { get; init; }
    public TestScheduler TestScheduler { get; init; } = new();
}