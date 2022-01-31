using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;

namespace NetDaemonApps.Tests;

public class HouseStateManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void HouseState_ShouldChangeToDay_WhenTimeIs9Am()
    {
        // Arrange
        var dayTime = TimeSpan.Parse("09:00:00");
        _ctx.InitHouseManagerApp();
        // Act
        _ctx.AdvanceTimeTo(dayTime.Ticks);
        // Assert
        _ctx.VerifyHouseState("Dag");
    }

    [Theory]
    [InlineData("2022-01-17 22:40:00", DayOfWeek.Monday)]
    [InlineData("2022-01-18 22:40:00", DayOfWeek.Tuesday)]
    [InlineData("2022-01-19 22:40:00", DayOfWeek.Wednesday)]
    [InlineData("2022-01-20 22:40:00", DayOfWeek.Thursday)]
    [InlineData("2022-01-21 23:40:00", DayOfWeek.Friday)]
    [InlineData("2022-01-22 23:40:00", DayOfWeek.Saturday)]
    [InlineData("2022-01-23 22:40:00", DayOfWeek.Sunday)]
    public void HouseState_ShouldChangeToNight_WhenDependingOnDayOfWeek(string nightTime, DayOfWeek dayOfWeek)
    {
        // Arrange
        var weekDayNightTime = DateTime.Parse(nightTime);

        _ctx.SetCurrentTime(weekDayNightTime.Date);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx.AdvanceTimeTo(weekDayNightTime.Ticks);

        // Assert
        _ctx.VerifyHouseState("Natt");
        weekDayNightTime.DayOfWeek.Should().Be(dayOfWeek);
    }

    [Fact]
    public void HouseState_ShouldChangeToMorning_WhenLightIsBrightAndWithinTimeLimits()
    {
        // Arrange
        _ctx.AdvanceTimeTo(TimeSpan.FromHours(5).Ticks);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Natt")
            .ChangeStateFor("sensor.light_outside")
                .FromState(34.9d)
                .ToState(35.0d);

        // Assert
        _ctx.VerifyHouseState("Morgon");
    }
    
    [Fact]
    public void HouseState_ShouldNotChangeToMorning_WhenLightIsBrightAndNotWithinLowerTimeLimits()
    {
        // Arrange
        _ctx.AdvanceTimeTo((new TimeSpan(4, 59, 59)).Ticks);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Natt")
            .ChangeStateFor("sensor.light_outside")
            .FromState(34.9d)
            .ToState(35.0d);

        // Assert
        
        // Check that no select option been called, i.e HouseState not set
        _ctx.HaContext.DidNotReceiveWithAnyArgs().CallService("input_select", "select_option", null, null);
    }
    
    [Fact]
    public void HouseState_ShouldNotChangeToMorning_WhenLightIsBrightAndNotWithinHigherTimeLimits()
    {
        // Arrange
        _ctx.AdvanceTimeTo((new TimeSpan(10, 0, 0)).Ticks);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Natt")
            .ChangeStateFor("sensor.light_outside")
            .FromState(34.9d)
            .ToState(35.0d);

        // Assert
        
        // Check that no select option been called, i.e HouseState not set
        _ctx.HaContext.DidNotReceiveWithAnyArgs().CallService("input_select", "select_option", null, null);
    }
    
    [Fact]
    public void HouseState_ShouldChangeToEvening_WhenLightIsDarkAndWithinTimeLimits()
    {
        // Arrange
        _ctx.AdvanceTimeTo(TimeSpan.FromHours(15).Ticks);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .ChangeStateFor("sensor.light_outside")
            .FromState(20.1d)
            .ToState(20.0d);

        // Assert
        _ctx.VerifyHouseState("Kväll");
    }
    
    [Fact]
    public void HouseState_ShouldNotChangeToEvening_WhenLightIsBrightAndNotWithinLowerTimeLimits()
    {
        // Arrange
        _ctx.AdvanceTimeTo((new TimeSpan(14, 59, 59)).Ticks);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .ChangeStateFor("sensor.light_outside")
            .FromState(20.1d)
            .ToState(20.0d);

        // Assert
        
        // Check that no select option been called, i.e HouseState not set
        _ctx.HaContext.DidNotReceiveWithAnyArgs().CallService("input_select", "select_option");
    }    
    
    [Fact]
    public void HouseState_ShouldNotChangeToEvening_WhenLightIsBrightAndNotWithinHigherTimeLimits()
    {
        // Arrange
        _ctx.AdvanceTimeTo((new TimeSpan(23, 00, 00)).Ticks);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .ChangeStateFor("sensor.light_outside")
            .FromState(20.1d)
            .ToState(20.0d);

        // Assert
        
        // Check that no select option been called, i.e HouseState not set
        _ctx.HaContext.DidNotReceiveWithAnyArgs().CallService("input_select", "select_option");
    }

    [Theory]
    [InlineData("dag", "Dag")]
    [InlineData("kvall", "Kväll")]
    [InlineData("morgon", "Morgon")]
    [InlineData("natt", "Natt")]
    [InlineData("stadning", "Städning")]
    public void HouseState_ShouldChangeCorrectly_WhenSceneIsActivated(string sceneName, string houseState)
    {
        // Arrange
        _ctx.InitHouseManagerApp();

        // Act
        _ctx.ActivateScene(sceneName);

        // Assert
        _ctx.VerifyHouseState(houseState);
    }
}

public static class HouseManagerAppTestContextInstanceExtensions
{
    public static (HouseStateManager, ILogger<HouseStateManager>) InitHouseManagerApp(this AppTestContext ctx)
    {
        var loggerMock = Substitute.For<ILogger<HouseStateManager>>();
        return (new HouseStateManager(ctx.HaContext, ctx.Scheduler, loggerMock), loggerMock);
    }

    public static void VerifyHouseState(this AppTestContext ctx, string houseState)
    {
        ctx.VerifyInputSelect_SelectOption("input_select.house_mode_select", houseState);
    }
}