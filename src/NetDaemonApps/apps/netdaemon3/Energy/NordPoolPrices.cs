using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetDaemon.Client;
using NetDaemon.Client.HomeAssistant.Model;

namespace NetDaemonApps;

internal static class NordPoolPrices
{
    public const string Area = "SE2";
    public const string Currency = "SEK";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    public static async Task<string> GetConfigEntryAsync(
        IHomeAssistantConnection connection,
        string priceSensor,
        CancellationToken cancellationToken)
    {
        var command = new GetEntityRegistryEntry
        {
            Type = "config/entity_registry/get",
            EntityId = priceSensor,
        };
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        request.CancelAfter(RequestTimeout);
        var response = await connection.SendCommandAndReturnResponseRawAsync<CommandMessage>(
            command, request.Token).ConfigureAwait(false);
        var entry = response?.Deserialize<RegistryEntry>();

        if (entry?.Platform != "nordpool" || string.IsNullOrWhiteSpace(entry.ConfigEntryId))
        {
            throw new InvalidOperationException($"No Nord Pool configuration entry was found for {priceSensor}.");
        }

        return entry.ConfigEntryId;
    }

    public static async Task<IReadOnlyList<ElectricityPriceInterval>> ReadDayAsync(
        NordpoolServices services,
        string configEntry,
        DateOnly date,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var response = await services.GetPricesForDateAsync(configEntry, date, [Area], Currency)
            .WaitAsync(RequestTimeout, cancellationToken).ConfigureAwait(false);

        if (response is not { ValueKind: JsonValueKind.Object } payload ||
            !payload.TryGetProperty(Area, out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Nord Pool returned no {Area} prices for {date:yyyy-MM-dd}.");
        }

        var prices = entries.Deserialize<PriceEntry[]>()!
            .Select(entry => new ElectricityPriceInterval(entry.Start, entry.End, entry.Price / 1000m))
            .OrderBy(quarter => quarter.Start)
            .ToArray();

        var expectedStart = start;
        foreach (var quarter in prices)
        {
            if (quarter.Start != expectedStart || quarter.End - quarter.Start != ElectricityPricePolicy.Quarter)
            {
                throw IncompleteDay();
            }

            expectedStart = quarter.End;
        }

        if (expectedStart != end)
        {
            throw IncompleteDay();
        }

        return prices;

        InvalidDataException IncompleteDay() =>
            new($"Nord Pool returned an incomplete or invalid {Area} quarter-hour series for {date:yyyy-MM-dd}.");
    }

    private sealed record GetEntityRegistryEntry : CommandMessage
    {
        [JsonPropertyName("entity_id")]
        public required string EntityId { get; init; }
    }

    private sealed record RegistryEntry
    {
        [JsonPropertyName("platform")]
        public string? Platform { get; init; }

        [JsonPropertyName("config_entry_id")]
        public string? ConfigEntryId { get; init; }
    }

    private sealed record PriceEntry
    {
        [JsonPropertyName("start")]
        public required DateTimeOffset Start { get; init; }

        [JsonPropertyName("end")]
        public required DateTimeOffset End { get; init; }

        [JsonPropertyName("price")]
        public required decimal Price { get; init; }
    }
}
