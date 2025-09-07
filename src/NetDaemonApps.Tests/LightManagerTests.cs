using Microsoft.Extensions.Logging;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;

namespace NetDaemonApps.Tests;

public class LightManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    // Tests removed - TomasRoomPir -> ElgatoKeyLight functionality is already implemented in TomasOffice.cs
}

public static class LigtManagerAppTestContextInstanceExtensions
{
    public static LightManager InitLightManager(this AppTestContext ctx, LightsConfiguration config)
    {
        var logger = Substitute.For<ILogger<LightManager>>();
        return new LightManager(ctx.HaContext,new FakeAppConfig<LightsConfiguration>(config), ctx.Scheduler, logger);
    }
}
