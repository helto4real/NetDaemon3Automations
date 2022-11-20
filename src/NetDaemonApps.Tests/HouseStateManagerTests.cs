using System;
using System.Globalization;
using FluentAssertions;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;

namespace NetDaemonApps.Tests;

public class HouseStateManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    private const string EveningBrightness = "20.0";
    private const string DayBrightness = "20.1";
    private const string MorningBrightness = "35.0";
    private const string NightBrightness = "34.9";
    private const int ChangeWaitTime = 15; // 15 minutes
    [Fact]
    public void HouseState_ShouldChangeToDay_WhenTimeIsMorningTime()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 8, 0, 0, DateTimeKind.Local).ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

        _ctx.InitHouseManagerApp();
        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(60).Ticks);
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
        var weekDayNightTime = DateTime.Parse(nightTime); //.ToUniversalTime();

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
        var dayTime = new DateTime(2022, 1, 1, 5, 0, 0, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);
        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Natt")
            .ChangeStateFor("sensor.light_outside")
                .FromState(NightBrightness )
                .ToState(MorningBrightness);

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);
        // Assert
        _ctx.VerifyHouseState("Morgon");
    }

    [Fact]
    public void HouseState_ShouldChangeToMorning_WhenLightIsBrightAndWithinTimeLimits2()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 5, 15, 0, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);
        _ctx.InitHouseManagerApp();
        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Natt")
            .ChangeStateFor("sensor.light_outside")
                .FromState(NightBrightness )
                .ToState(MorningBrightness);

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);

        // _ctx
        //     .ChangeStateFor("sensor.light_outside")
        //     .FromState(MorningBrightness )
        //     .ToState("36.8");
        // _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);


        _ctx.VerifyHouseState("Morgon");
        // Assert
    }

    [Fact]
    public void HouseState_ShouldChangeToMorning_WhenLightIsBrightAndTimeIsTurningWithinLimits()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 5, 0, 0, DateTimeKind.Local).ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Natt")
            .WithEntityState("sensor.light_outside", MorningBrightness);

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);
        // Assert
        _ctx.VerifyHouseState("Morgon");
    }
    
    [Fact]
    public void HouseState_ShouldNotChangeToMorning_WhenLightIsBrightAndNotWithinLowerTimeLimits()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 4, 59, 59, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);
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
        var dayTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

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
        var dayTime = new DateTime(2022, 1, 1, 16, 0, 0, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .ChangeStateFor("sensor.light_outside")
            .FromState(DayBrightness)
            .ToState(EveningBrightness);

        // It should still be day
        _ctx.VerifyHouseStateNotChanged();
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(ChangeWaitTime).Ticks);
        // Assert
        _ctx.VerifyHouseState("Kväll");
    }

    [Fact]
    public void HouseState_ShouldChangeToEvening_WhenLightIsDarkAndTimeIsTurningWithinLimits()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .WithEntityState("sensor.light_outside", EveningBrightness);

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);
        // Assert
        _ctx.VerifyHouseState("Kväll");
    }

    
    [Fact]
    public void HouseState_ShouldNotChangeToEvening_WhenLightIsBrightAndNotWithinLowerTimeLimits()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 14, 59, 59, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .ChangeStateFor("sensor.light_outside")
            .FromState(DayBrightness)
            .ToState(EveningBrightness);

        // Assert
        
        _ctx.VerifyHouseStateNotChanged();
    }    
    
    [Fact]
    public void HouseState_ShouldNotChangeToEvening_WhenLightIsBrightAndNotWithinHigherTimeLimits()
    {
        // Arrange
        var dayTime = new DateTime(2022, 1, 1, 23, 0, 0, DateTimeKind.Local); //.ToUniversalTime();
        _ctx.SetCurrentTime(dayTime);

        _ctx.InitHouseManagerApp();

        // Act
        _ctx
            .WithEntityState("input_select.house_mode_select", "Dag")
            .ChangeStateFor("sensor.light_outside")
            .FromState(DayBrightness)
            .ToState(EveningBrightness);

        // Assert
        
        _ctx.VerifyHouseStateNotChanged();
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
        return (
            new HouseStateManager(new Entities(ctx.HaContext), ctx.Scheduler, loggerMock,
                new FakeAppConfig<GlobalConfig>(ctx.TestConfig)), loggerMock);
    }

    public static void VerifyHouseState(this AppTestContext ctx, string houseState)
    {
        ctx.VerifyInputSelect_SelectOption("input_select.house_mode_select", houseState);
    }

    public static void VerifyHouseStateNotChanged(this AppTestContext ctx)
    {
        ctx.VerifyInputSelect_SelectOption_NotChanged("input_select.house_mode_select");
    }
}
