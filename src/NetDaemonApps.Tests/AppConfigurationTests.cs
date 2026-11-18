using System.IO;
using System.Reactive.Concurrency;
using FluentAssertions;
using HomeAssistantGenerated;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using NetDaemon.AppModel;
using NetDaemon.Client;
using NetDaemon.Extensions.Tts;
using NetDaemon.HassModel;
using NSubstitute;

namespace NetDaemonApps.Tests;

public sealed class AppConfigurationTests
{
    [Theory]
    [InlineData(typeof(PrinterConfig))]
    [InlineData(typeof(CalendarAnnouncementConfig))]
    [InlineData(typeof(ElectricityPriceTipsConfig))]
    [InlineData(typeof(EventStreamWatchdogConfig))]
    [InlineData(typeof(GlobalConfig))]
    [InlineData(typeof(LightsConfiguration))]
    [InlineData(typeof(SpotifyConfig))]
    [InlineData(typeof(TvConfig))]
    [InlineData(typeof(WelcomeConfig))]
    [InlineData(typeof(ElinsOfficeConfig))]
    [InlineData(typeof(TomasOfficeConfig))]
    public void Configuration_ShouldExistUnderFullTypeName_WhenPublishedYamlIsLoaded(Type configType)
    {
        using var configuration = LoadConfiguration();

        configuration.GetSection(configType.FullName!).Exists().Should().BeTrue();
    }

    [Fact]
    public void Configuration_ShouldBindRequiredEntities_WhenResolvedThroughNetDaemon()
    {
        using var configuration = LoadConfiguration();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(Substitute.For<IHaContext>())
            .AddNetDaemonAppModel()
            .AddHomeAssistantGenerated()
            .BuildServiceProvider();
        using var scope = services.CreateScope();

        var lights = scope.ServiceProvider.GetRequiredService<IAppConfig<LightsConfiguration>>().Value;
        var welcome = scope.ServiceProvider.GetRequiredService<IAppConfig<WelcomeConfig>>().Value;
        var office = scope.ServiceProvider.GetRequiredService<IAppConfig<TomasOfficeConfig>>().Value;
        var electricity = scope.ServiceProvider.GetRequiredService<IAppConfig<ElectricityPriceTipsConfig>>().Value;

        lights.LivingRoomPirs.Should().HaveCount(2);
        lights.TvRoomPirs.Should().HaveCount(3);
        lights.KitchenPir.EntityId.Should().Be("binary_sensor.kok_pir_occupancy");
        welcome.HallwayMediaPlayer.EntityId.Should().Be("media_player.vardagsrum");
        welcome.DoorSensor.EntityId.Should().Be("binary_sensor.hall_dorr_sensor_contact");
        welcome.Greetings.Should().HaveCount(20);
        office.JarvisMuteSwitch.EntityId.Should().Be("switch.jarvis_tysta");
        electricity.MinimumWindow.Should().Be(TimeSpan.FromHours(2));
        electricity.LowPriceSekPerKwh.Should().Be(0.30m);
    }

    [Fact]
    public void ElectricityPriceTipsApp_ShouldActivate_WithNetDaemonRegistrations()
    {
        using var configuration = LoadConfiguration();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(Substitute.For<IHaContext>())
            .AddSingleton(Substitute.For<IHomeAssistantRunner>())
            .AddSingleton(Substitute.For<ITextToSpeechService>())
            .AddSingleton<IScheduler>(new TestScheduler())
            .AddNetDaemonAppModel()
            .AddHomeAssistantGenerated()
            .BuildServiceProvider();
        using var scope = services.CreateScope();

        using var app = ActivatorUtilities.CreateInstance<ElectricityPriceTipsApp>(scope.ServiceProvider);
    }

    private static ConfigurationManager LoadConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration.AddYamlAppConfig(Path.Combine(AppContext.BaseDirectory, "apps"));
        return configuration;
    }
}
