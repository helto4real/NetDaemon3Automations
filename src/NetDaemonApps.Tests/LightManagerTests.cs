using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;

namespace NetDaemonApps.Tests;

public class LightManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void LightManager_ShouldTurnOnKeyLight_WhenEnterRoom()
    {
        // Arrange
        var config = new LightsConfiguration
        {
            TomasRoomPir = _ctx.GetEntity<BinarySensorEntity>( "binary_sensor.tomas_rum_pir"),
            ElgatoKeyLight = _ctx.GetEntity<LightEntity>( "light.elgato_key_light", "off")
        };
        
        _ctx.InitLightManager(config);
        // Act
        _ctx
            .ChangeStateFor("binary_sensor.tomas_rum_pir")
            .FromState("off")
            .ToState("on");
        // Assert
        config.ElgatoKeyLight!.VerifyCallService("turn_on");
    }
    
    [Fact]
    public void LightManager_ShouldTurnOffKeyLight_WhenWhenNonDetectedIn30Minutes()
    {
        // Arrange
        var config = new LightsConfiguration
        {
            TomasRoomPir = _ctx.GetEntity<BinarySensorEntity>( "binary_sensor.tomas_rum_pir"),
            ElgatoKeyLight = _ctx.GetEntity<LightEntity>( "light.elgato_key_light", "off")
        };
        
        _ctx.InitLightManager(config);
        // Act
        _ctx
            .ChangeStateFor("binary_sensor.tomas_rum_pir")
            .FromState("off")
            .ToState("on");
        // Assert
        config.ElgatoKeyLight!.VerifyCallService("turn_on");
    }
}

public static class LigtManagerAppTestContextInstanceExtensions
{
    public static LightManager InitLightManager(this AppTestContext ctx, LightsConfiguration config)
    {
        var logger = Substitute.For<ILogger<LightManager>>();
        return new LightManager(ctx.HaContext,new FakeAppConfig<LightsConfiguration>(config), ctx.Scheduler, logger);
    }
}
