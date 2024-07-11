using System;
using Microsoft.Reactive.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NetDaemon.HassModel;

namespace NetDaemonApps.Tests.Helpers;

/// <summary>
///     Helper class to handle state of the test session
/// </summary>
public class AppTestContext
{
    public HaContextMock HaContextMock { get; } = new();
    public IHaContext HaContext => HaContextMock.HaContext;
    public TestScheduler Scheduler { get; } = new ();
    public ILogger Logger { get; } = Substitute.For<ILogger>();
    public GlobalConfig TestConfig { get; } = new GlobalConfig()
    {
        DayTime = TimeSpan.Parse("09:00:00"),
        MorningTime = TimeSpan.Parse("09:00:00"),
        NightTimeWeekdays = TimeSpan.Parse("22:40:00"),
        NightTimeWeekends = TimeSpan.Parse("23:40:00")
    };

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
        Scheduler.AdvanceTo(absoluteTime);
    }

    public void AdvanceTimeBy(long absoluteTime)
    {
        Scheduler.AdvanceBy(absoluteTime);
    }

    public void SetCurrentTime(DateTime time)
    {
        AdvanceTimeTo(time.Ticks);
    }
}
