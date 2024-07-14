
using System;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;

namespace NetDaemonApps.Tests;

public class RoomPresenceTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void RoomPresence_ShouldTurnOnBleTransmitter_WhenTomasIsStateHome()
    {
        // Arrange
        _ctx.InitRoomPresence();
        
        // Act
        _ctx
            .ChangeStateFor("person.tomas")
            .FromState("not_home")
            .ToState("Hemma");
        // Assert
        _ctx.VerifyBleTransmitterServiceCallCommand("turn_on");
    }

    [Fact]
    public void RoomPresence_ShouldTurnOnBleTransmitter_WhenTomasArrivesHome()
    {
        // Arrange
        _ctx.InitRoomPresence();
        
        // Act
        _ctx
            .ChangeStateFor("person.tomas")
            .FromState("not_home")
            .ToState("Nyss anlänt");
        // Assert
        _ctx.VerifyBleTransmitterServiceCallCommand("turn_on");
    }

    [Fact]
    public void RoomPresence_ShouldNotTurnOnBleTransmitter_WhenStateFromArrivesToHome()
    {
        // Arrange
        _ctx.InitRoomPresence();
        
        // Act
        _ctx
            .ChangeStateFor("person.tomas")
            .FromState("Nyss anlänt")
            .ToState("Hemma");
        // Assert
        _ctx.VerifyBleTransmitterServiceNotCallCommand();
    }

    [Fact]
    public void RoomPresence_ShouldTurnOnBleTransmitter_WhenTomasLeavesHome()
    {
        // Arrange
        _ctx.InitRoomPresence();
        
        // Act
        _ctx
            .ChangeStateFor("person.tomas")
            .FromState("Hemma")
            .ToState("not_home");
        // Assert
        _ctx.VerifyBleTransmitterServiceCallCommand("turn_off");
    }

    [Fact]
    public void RoomPresence_ShouldTurnOffBleTransmitter_WhenTomasIsInBedRoomDuringNight()
    {
        // Arrange
        // make sure we have state night
        _ctx
            .ChangeStateFor("input_select.house_mode_select")
            .FromState("Natt")
            .ToState("Natt");  

        _ctx.InitRoomPresence();
        // Act
        _ctx
            .ChangeStateFor("sensor.tomas_klocka_ble")
            .FromState("not_home")
            .ToState("sovrummet");
        // Pass 25 minutes
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromMinutes(25).Ticks);
        // Assert
        _ctx.VerifyBleTransmitterServiceCallCommand("turn_off");
    }

    [Fact]
    public void RoomPresence_ShouldTurnOffBleTransmitter_WhenDuringNightAndTomasIsInBedRoom()
    {
        // Arrange
        // make sure Tomas is in bedroom state 
        _ctx
            .ChangeStateFor("sensor.tomas_klocka_ble")
            .FromState("not_home")
            .ToState("sovrummet");

        _ctx.InitRoomPresence();
        // Act
        _ctx
            .ChangeStateFor("input_select.house_mode_select")
            .FromState("Evening")
            .ToState("Natt");  

        // Pass 25 minutes
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromMinutes(25).Ticks);
        // Assert
        _ctx.VerifyBleTransmitterServiceCallCommand("turn_off");
    }

    [Fact]
    public void RoomPresence_ShouldTurnOnBleTransmitter_WhenClockTurn7InTheMorning()
    {
        // Arrange
        // make sure Tomas is Home
        _ctx
            .ChangeStateFor("person.tomas")
            .FromState("not_home")
            .ToState("Hemma");
        // make sure Tomas is in bedroom state 
        _ctx.InitRoomPresence();
        // Act
        // Advance time to 7 in the morning
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(7).Ticks);
        // Assert
        _ctx.VerifyBleTransmitterServiceCallCommand("turn_on");
    }
}

public static class RoomPresenceAppTestContextInstanceExtensions
{
    public static RoomPresence InitRoomPresence(this AppTestContext ctx)
    {
        var logger = Substitute.For<ILogger<RoomPresence>>();
        var services = new Services(ctx.HaContext);
        var entities = new Entities(ctx.HaContext);
        return new RoomPresence(services, entities, logger, ctx.Scheduler);
    }

    public static void VerifyBleTransmitterServiceCallCommand(this AppTestContext ctx, string command)
    {
        ctx.VerifyCallServiceWithData("notify.mobile_app_galaxy_watch6_classic_jq4a", null,
                new NotifyMobileAppGalaxyWatch6ClassicJq4aParameters { Message = "command_ble_transmitter", Data = new { command } } , 1);
    }

    public static void VerifyBleTransmitterServiceNotCallCommand(this AppTestContext ctx)
    {
        ctx.VerifyNotCallService("notify.mobile_app_galaxy_watch6_classic_jq4a");
    }
}

// CallService("notify", *"mobile_app_galaxy_watch6_classic_jq4a"*, <null>, *NotifyMobileAppGalaxyWatch6ClassicJq4aParameters { Message = command_ble_transmitter, Title = , Target = , Data = { command = turn_on } }*)
