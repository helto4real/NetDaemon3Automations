using FluentAssertions;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.HassModel.Entities;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;
using System.Linq;
using System.Text.Json;

namespace NetDaemonApps.Tests;

public sealed class CriticalAutomationBehaviorTests
{
    [Fact]
    public async Task EventStreamWatchdog_ShouldCreateOnePersistentNotification_WhenEventsStop()
    {
        var context = AppTestContext.New();
        var scheduler = Substitute.For<INetDaemonScheduler>();
        var timeProvider = new AdjustableTimeProvider(new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero));
        Action? checkEventStream = null;
        scheduler.RunEvery(TimeSpan.FromMinutes(1), Arg.Do<Action>(action => checkEventStream = action))
            .Returns(Substitute.For<IDisposable>());

        var app = new EventStreamWatchdog(
            context.HaContext,
            scheduler,
            timeProvider,
            Substitute.For<ILogger<EventStreamWatchdog>>(),
            new FakeAppConfig<EventStreamWatchdogConfig>(new EventStreamWatchdogConfig()));
        await app.InitializeAsync(CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(6));
        checkEventStream.Should().NotBeNull();
        checkEventStream!();
        checkEventStream();

        context.HaContext.Received(1).CallService(
            "persistent_notification",
            "create",
            Arg.Any<ServiceTarget?>(),
            Arg.Any<object?>());
    }

    [Fact]
    public async Task LightManager_ShouldUsePercentageBrightness_WhenEveningStarts()
    {
        var context = AppTestContext.New();
        var configuration = new LightsConfiguration
        {
            LivingRoomPirs = [new BinarySensorEntity(context.HaContext, "binary_sensor.living_room")],
            TvRoomPirs = [new BinarySensorEntity(context.HaContext, "binary_sensor.tv_room")],
            KitchenPir = new BinarySensorEntity(context.HaContext, "binary_sensor.kitchen"),
            RemoteTvRummet = new RemoteEntity(context.HaContext, "remote.tvrummet"),
            HouseModeSelect = new InputSelectEntity(context.HaContext, "input_select.house_mode_select"),
            CommandDelay = TimeSpan.Zero,
        };
        var app = new LightManager(
            context.HaContext,
            new FakeAppConfig<LightsConfiguration>(configuration),
            context.Scheduler,
            Substitute.For<ILogger<LightManager>>(),
            new CommandSequencer(TimeProvider.System),
            TimeProvider.System);
        await app.InitializeAsync(CancellationToken.None);

        context.ChangeStateFor("input_select.house_mode_select")
            .FromState(HouseModeCodec.Day)
            .ToState(HouseModeCodec.Evening);

        context.HaContext.Received().CallService(
            "light",
            "turn_on",
            Arg.Is<ServiceTarget>(target => target.EntityIds!.Contains("light.vardagsrum")),
            Arg.Is<LightTurnOnParameters>(data => data.BrightnessPct == 59));
    }

    [Fact]
    public async Task PrinterApp_ShouldUseHighFanSpeed_WhenHazardousFilamentStartsPrinting()
    {
        var context = AppTestContext.New();
        context.WithEntityState("sensor.h2d_0948bb4c2200520_active_tray", "Bambu ASA");
        var app = new PrinterApp(
            new Entities(context.HaContext),
            new Services(context.HaContext),
            Substitute.For<ITextToSpeechService>(),
            context.Scheduler,
            Substitute.For<ILogger<PrinterApp>>(),
            new FakeAppConfig<PrinterConfig>(new PrinterConfig()));
        await app.InitializeAsync(CancellationToken.None);

        context.ChangeStateFor("sensor.h2d_print_status")
            .FromState("idle")
            .ToState(HomeAssistantStates.Running);

        context.HaContext.Received(1).CallService(
            "fan",
            "turn_on",
            Arg.Is<ServiceTarget>(target => target.EntityIds!.Contains("fan.gym_luftrenare")),
            Arg.Is<FanTurnOnParameters>(data => data.Percentage == 75));
    }

    [Fact]
    public async Task WelcomeHomeManager_ShouldGreetPerson_WhenDoorIsOpenAndPresenceChanges()
    {
        var context = AppTestContext.New();
        context.WithEntityState("binary_sensor.front_door", HomeAssistantStates.On);
        var textToSpeech = Substitute.For<ITextToSpeechService>();
        var configuration = new WelcomeConfig
        {
            PresenceCriteria = "_presence",
            HallwayMediaPlayer = new MediaPlayerEntity(context.HaContext, "media_player.hallway"),
            DoorSensor = new BinarySensorEntity(context.HaContext, "binary_sensor.front_door"),
            Greetings = ["Welcome {namn}"],
        };
        var app = new WelcomeHomeManager(
            context.HaContext,
            Substitute.For<ILogger<WelcomeHomeManager>>(),
            textToSpeech,
            new FakeAppConfig<WelcomeConfig>(configuration),
            TimeProvider.System);
        await app.InitializeAsync(CancellationToken.None);

        context.ChangeStateFor("device_tracker.tomas_presence")
            .FromState("away")
            .ToState(PresenceStates.JustArrived);

        textToSpeech.Received(1).Speak(
            "media_player.hallway",
            "Welcome tomas",
            "google_translate_say");
    }

    [Fact]
    public async Task TvManager_ShouldNotTurnOnRoomLights_WhenTelevisionWasAlreadyDark()
    {
        var context = AppTestContext.New();
        context.WithEntityState("light.tvrummet", HomeAssistantStates.Off)
            .WithEntityState("input_select.house_mode_select", HouseModeCodec.Night);
        var app = new TvManager(
            new Entities(context.HaContext),
            Substitute.For<ILogger<TvManager>>(),
            context.Scheduler,
            new CommandSequencer(TimeProvider.System),
            new FakeAppConfig<TvConfig>(new TvConfig { CommandDelay = TimeSpan.Zero }));
        await app.InitializeAsync(CancellationToken.None);

        context.ChangeStateFor("remote.tvrummet")
            .FromHassState(RemoteState("TV"))
            .ToHassState(RemoteState("PowerOff"));

        context.VerifyNotCallService("light.turn_on");
    }

    private static EntityState RemoteState(string activity) => new()
    {
        EntityId = "remote.tvrummet",
        State = HomeAssistantStates.On,
        AttributesJson = JsonSerializer.SerializeToElement(new { current_activity = activity }),
    };

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
