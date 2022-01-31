using System;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;

namespace NetDaemonApps.Tests.Helpers;

/// <summary>
///     Helper class to handle state of the test session
/// </summary>
public class AppTestContext
{
    public HaContextMock HaContextMock { get; } = new();
    public IHaContext HaContext => HaContextMock.HaContext;
    public NetDaemonSchedulerMock SchedulerMock { get; } = new();
    public INetDaemonScheduler Scheduler => SchedulerMock.Scheduler;

    public static AppTestContext New()
    {
        return new();
    }

    public static AppTestContext New(DateTime startTime)
    {
        var ctx = new AppTestContext();
        ctx.SetCurrentTime(startTime);
        return ctx;
    }

    public void AdvanceTimeTo(long absoluteTime)
    {
        SchedulerMock.TestScheduler.AdvanceTo(absoluteTime);
    }

    public void AdvanceTimeBy(long absoluteTime)
    {
        SchedulerMock.TestScheduler.AdvanceBy(absoluteTime);
    }

    public void SetCurrentTime(DateTime time)
    {
        AdvanceTimeTo(time.Ticks);
    }
}
