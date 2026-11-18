using System.Linq;
using System.Text.Json;
using FluentAssertions;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using NetDaemon.Client;
using NetDaemon.Client.HomeAssistant.Model;
using NetDaemon.Extensions.Tts;
using NetDaemon.HassModel.Entities;
using NetDaemonApps.Tests.Helpers;
using NSubstitute;

namespace NetDaemonApps.Tests;

public sealed class ElectricityPriceTipsAppTests
{
    private static readonly DateTimeOffset DayStart = new(2026, 11, 18, 0, 0, 0, TimeSpan.FromHours(1));
    private const string Presence = "zone.home";
    private const string HouseModeSelect = "input_select.house_mode_select";
    private const string TomorrowAvailable = "binary_sensor.nord_pool_se2_morgondagens_pris_tillgangligt";
    private const string SensorPath = "states/sensor.nasta_billiga_elperiod";

    [Fact]
    public async Task InitializeAsync_ShouldAnnounceOnce_WhenSomeoneArrivesDuringCheapPeriod()
    {
        var fixture = new Fixture(DayStart.AddHours(15), DayPrices());
        using var app = fixture.App;
        await app.InitializeAsync(CancellationToken.None);

        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);
        var state = fixture.PublishedStates.Should().ContainSingle().Subject;
        state.GetProperty("state").GetDateTimeOffset().Should().Be(DayStart.AddHours(14));
        state.GetProperty("attributes").GetProperty("end").GetDateTimeOffset().Should().Be(DayStart.AddHours(18));
        state.GetProperty("attributes").GetProperty("average_price_sek_per_kwh").GetDecimal().Should().Be(0.20m);

        fixture.Context.ChangeStateFor(Presence).FromState(0).ToState(1);
        fixture.Context.ChangeStateFor(Presence).FromState(1).ToState(2);
        fixture.Context.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);

        fixture.Speech.Received(1).Speak(
            "media_player.huset", "Nu är det läge att köra disk eller tvätt. Elen är billig fram till klockan 18:00.", "cloud_say");
        await fixture.Context.HaContext.Received(1).CallServiceWithResponseAsync(
            "nordpool", "get_prices_for_date", null,
            Arg.Is<NordpoolGetPricesForDateParameters>(data =>
                (string?)data.ConfigEntry == "my-nordpool-entry" && (string?)data.Currency == "SEK" &&
                data.Date == new DateOnly(2026, 11, 18) && data.Areas!.Contains("SE2")));
    }

    [Fact]
    public async Task InitializeAsync_ShouldAnnounceAtStart_WhenSomeoneIsAlreadyHome()
    {
        var fixture = new Fixture(DayStart.AddHours(13).AddMinutes(59), DayPrices());
        using var app = fixture.App;
        fixture.Context.WithEntityState(Presence, 1);
        await app.InitializeAsync(CancellationToken.None);
        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);

        fixture.Context.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);

        fixture.Speech.Received(1).Speak("media_player.huset", Arg.Is<string>(text => text.StartsWith("Nu är det läge")), "cloud_say");
    }

    [Fact]
    public async Task InitializeAsync_ShouldStaySilent_WhenTooLittleTimeRemainsOnArrival()
    {
        var fixture = new Fixture(DayStart.AddHours(16).AddMinutes(1), DayPrices());
        using var app = fixture.App;
        await app.InitializeAsync(CancellationToken.None);

        fixture.Context.ChangeStateFor(Presence).FromState(0).ToState(1);

        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);
        fixture.PublishedStates.Should().ContainSingle().Which.GetProperty("state").GetString().Should().Be("unavailable");
    }

    [Fact]
    public async Task InitializeAsync_ShouldAnnounceNightTipOnce_WhenSomeoneArrivesAfterEveningCheck()
    {
        var fixture = new Fixture(DayStart.AddHours(19), DayPrices(), NightPrices());
        using var app = fixture.App;
        await app.InitializeAsync(CancellationToken.None);
        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);

        fixture.Context.AdvanceTimeBy(TimeSpan.FromHours(3).Ticks);
        fixture.Context.ChangeStateFor(Presence).FromState(0).ToState(1);
        fixture.Context.ChangeStateFor(Presence).FromState(1).ToState(2);

        fixture.Speech.Received(1).Speak("media_player.huset",
            "Om ni vill köra en maskin i natt är elen billigast mellan klockan 02:00 och 04:00.", "cloud_say");
    }

    [Fact]
    public async Task InitializeAsync_ShouldStaySilent_WhenTheWindowWasAnnouncedBeforeAppRestart()
    {
        var fixture = new Fixture(DayStart.AddHours(15), DayPrices());
        using var app = fixture.App;
        fixture.Context.WithEntityState(Presence, 1);
        var persisted = new EntityState
        {
            EntityId = "sensor.nasta_billiga_elperiod",
            AttributesJson = JsonSerializer.SerializeToElement(new { last_day_tip_end = DayStart.AddHours(18) }),
        };
        fixture.Context.HaContext.GetState(persisted.EntityId).Returns(persisted);

        await app.InitializeAsync(CancellationToken.None);

        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);
        fixture.PublishedStates.Should().ContainSingle().Which.GetProperty("attributes")
            .GetProperty("last_day_tip_end").GetDateTimeOffset().Should().Be(DayStart.AddHours(18));
    }

    [Fact]
    public async Task InitializeAsync_ShouldKeepSpeakersSilent_WhenSomeoneArrivesDuringNightMode()
    {
        var fixture = new Fixture(DayStart.AddHours(23), DayPrices(), NightPrices());
        using var app = fixture.App;
        fixture.Context.WithEntityState(HouseModeSelect, HouseModeCodec.Night);
        await app.InitializeAsync(CancellationToken.None);

        fixture.Context.ChangeStateFor(Presence).FromState(0).ToState(1);
        fixture.Context.AdvanceTimeBy(TimeSpan.FromHours(3).Ticks);

        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);
    }

    [Fact]
    public async Task InitializeAsync_ShouldSuppressNightTip_WhenTomorrowPricesAreMissing()
    {
        var fixture = new Fixture(DayStart.AddHours(19), DayPrices());
        using var app = fixture.App;
        fixture.Context.WithEntityState(Presence, 1);
        await app.InitializeAsync(CancellationToken.None);

        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);
    }

    [Fact]
    public async Task InitializeAsync_ShouldPublishAllCheapPeriodsClippedToNext24Hours()
    {
        var now = DayStart.AddHours(17);
        var today = PriceResponse(DayStart, 96, index => index is >= 64 and < 80 ? 200m : 1000m);
        var tomorrow = PriceResponse(DayStart.AddDays(1), 96,
            index => index is >= 8 and < 16 or >= 56 and < 84 ? 150m : 1200m);
        var fixture = new Fixture(now, today, tomorrow);
        using var app = fixture.App;

        await app.InitializeAsync(CancellationToken.None);

        var attributes = fixture.PublishedStates.Should().ContainSingle().Which.GetProperty("attributes");
        attributes.GetProperty("prices_until").GetDateTimeOffset().Should().Be(DayStart.AddDays(2));
        var periods = attributes.GetProperty("periods_24h").EnumerateArray().Select(period => (
            period.GetProperty("start").GetDateTimeOffset(),
            period.GetProperty("end").GetDateTimeOffset()));
        periods.Should().Equal(
            (now, DayStart.AddHours(20)),
            (DayStart.AddHours(26), DayStart.AddHours(28)),
            (DayStart.AddHours(38), now.AddHours(24)));
        var prices = attributes.GetProperty("prices_24h").EnumerateArray().ToArray();
        prices.Should().HaveCount(96);
        prices[0].GetProperty("start").GetDateTimeOffset().Should().Be(now);
        prices[0].GetProperty("price_ore_per_kwh").GetDecimal().Should().Be(20m);
        prices[^1].GetProperty("end").GetDateTimeOffset().Should().Be(now.AddHours(24));
    }

    [Fact]
    public async Task InitializeAsync_ShouldPublishPartialCoverage_WhenTomorrowPricesAreMissing()
    {
        var fixture = new Fixture(DayStart.AddHours(17), DayPrices());
        using var app = fixture.App;

        await app.InitializeAsync(CancellationToken.None);

        var attributes = fixture.PublishedStates.Should().ContainSingle().Which.GetProperty("attributes");
        attributes.GetProperty("prices_until").GetDateTimeOffset().Should().Be(DayStart.AddDays(1));
        attributes.GetProperty("periods_24h").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_ShouldUpdateForecast_WhenTomorrowPricesBecomeAvailable()
    {
        var fixture = new Fixture(DayStart.AddHours(11), DayPrices());
        using var app = fixture.App;
        await app.InitializeAsync(CancellationToken.None);

        fixture.TomorrowResponse = NightPrices();
        fixture.Context.ChangeStateFor(TomorrowAvailable)
            .FromState(HomeAssistantStates.Off).ToState(HomeAssistantStates.On);

        fixture.PublishedStates.Should().HaveCount(2);
        var attributes = fixture.PublishedStates[1].GetProperty("attributes");
        attributes.GetProperty("prices_until").GetDateTimeOffset().Should().Be(DayStart.AddDays(2));
        attributes.GetProperty("periods_24h").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRetryPricesAndSpeak_WhenAnIncompleteDayIsReplaced()
    {
        var fixture = new Fixture(DayStart.AddHours(14), DayPrices(95));
        using var app = fixture.App;
        fixture.Context.WithEntityState(Presence, 1);
        await app.InitializeAsync(CancellationToken.None);
        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);

        fixture.TodayResponse = DayPrices();
        fixture.Context.AdvanceTimeBy(TimeSpan.FromMinutes(14).Ticks);
        fixture.Speech.DidNotReceiveWithAnyArgs().Speak(default!, default!, default!);
        fixture.Context.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);

        fixture.Speech.Received(1).Speak("media_player.huset", Arg.Is<string>(text => text.StartsWith("Nu är det läge")), "cloud_say");
    }

    [Theory]
    [InlineData(2026, 3, 29, 1, 92)]
    [InlineData(2026, 10, 25, 2, 100)]
    public async Task InitializeAsync_ShouldAcceptFullPriceDay_WhenStockholmChangesClocks(
        int year, int month, int day, int utcOffset, int quarters)
    {
        var start = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.FromHours(utcOffset));
        var fixture = new Fixture(start.AddHours(9), PriceResponse(start, quarters, _ => -100m));
        using var app = fixture.App;
        fixture.Context.WithEntityState(Presence, 1);
        await app.InitializeAsync(CancellationToken.None);

        fixture.Speech.Received(1).Speak("media_player.huset", Arg.Is<string>(text => text.StartsWith("Nu är det läge")), "cloud_say");
        fixture.PublishedStates.Should().ContainSingle().Which
            .GetProperty("attributes").GetProperty("average_price_sek_per_kwh").GetDecimal().Should().Be(-0.10m);
    }

    private static JsonElement DayPrices(int quarters = 96) =>
        PriceResponse(DayStart, quarters, index => index is >= 56 and < 72 ? 200m : 1000m);

    private static JsonElement NightPrices() =>
        PriceResponse(DayStart.AddDays(1), 96, index => index is >= 8 and < 16 ? 150m : 1200m);

    private static JsonElement PriceResponse(DateTimeOffset start, int quarters, Func<int, decimal> price) =>
        JsonSerializer.SerializeToElement(new
        {
            SE2 = Enumerable.Range(0, quarters).Select(index => new
            {
                start = start.AddMinutes(15 * index).ToUniversalTime(),
                end = start.AddMinutes(15 * (index + 1)).ToUniversalTime(),
                price = price(index),
            }),
        });

    private sealed class Fixture
    {
        public AppTestContext Context { get; }
        public ITextToSpeechService Speech { get; } = Substitute.For<ITextToSpeechService>();
        public ElectricityPriceTipsApp App { get; }
        public List<JsonElement> PublishedStates { get; } = [];
        public JsonElement TodayResponse { get; set; }
        public JsonElement? TomorrowResponse { get; set; }

        public Fixture(DateTimeOffset now, JsonElement today, JsonElement? tomorrow = null)
        {
            Context = AppTestContext.New(now.UtcDateTime);
            TodayResponse = today;
            TomorrowResponse = tomorrow;
            Context.WithEntityState(Presence, 0);
            Context.WithEntityState(HouseModeSelect, HouseModeCodec.Day);
            Context.WithEntityState(TomorrowAvailable, tomorrow is null ? HomeAssistantStates.Off : HomeAssistantStates.On);

            var connection = Substitute.For<IHomeAssistantConnection>();
            connection.SendCommandAndReturnResponseRawAsync<CommandMessage>(
                    Arg.Is<CommandMessage>(command => command.Type == "config/entity_registry/get" &&
                        command.GetJsonString().Contains("sensor.nord_pool_se2_aktuellt_pris")),
                    Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.SerializeToElement(new { platform = "nordpool", config_entry_id = "my-nordpool-entry" }));
            connection.PostApiCallAsync<JsonElement?>(SensorPath, Arg.Any<CancellationToken>(),
                    Arg.Do<object?>(value => PublishedStates.Add(JsonSerializer.SerializeToElement(value))))
                .Returns(JsonSerializer.SerializeToElement(new { entity_id = "sensor.nasta_billiga_elperiod" }));

            var requestedDay = DateOnly.FromDateTime(now.DateTime);
            Context.HaContext.CallServiceWithResponseAsync(
                    "nordpool", "get_prices_for_date", Arg.Any<ServiceTarget?>(), Arg.Any<NordpoolGetPricesForDateParameters>())
                .Returns(call =>
                {
                    var parameters = call.Arg<NordpoolGetPricesForDateParameters>();
                    return parameters.Date == requestedDay ? TodayResponse : TomorrowResponse;
                });

            var runner = Substitute.For<IHomeAssistantRunner>();
            runner.CurrentConnection.Returns(connection);
            App = new ElectricityPriceTipsApp(Context.HaContext, new Entities(Context.HaContext), new NordpoolServices(Context.HaContext),
                runner, Speech, Context.Scheduler, Substitute.For<ILogger<ElectricityPriceTipsApp>>(),
                new FakeAppConfig<ElectricityPriceTipsConfig>(new ElectricityPriceTipsConfig()));
        }
    }
}
