using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NetDaemon.Extensions.Persistance;

public static class DataStorageExtensions
{
    public static IServiceCollection AddStateRepository(this IServiceCollection services)
    {
        services.AddSingleton<StateRepository>();
        services.AddSingleton<IStateRepository>(s => s.GetRequiredService<StateRepository>());
        services.AddScoped(typeof(IPersistState<>), typeof(PersistState<>));
        return services;
    }
}

/// <summary>
///     Loads any saved state at app constructor
/// </summary>
/// <typeparam name="T">Type ot load</typeparam>
public interface IPersistState<T> where T : class
{
    T Value { get; set; }
}

public class PersistState<T> : IPersistState<T>, IDisposable where T : class, new()
{
    private readonly IStateRepository _repository;
    private readonly string _uniqueId;
    public T Value { get; set; }
    public PersistState(IStateRepository repository)
    {
        _repository = repository;
        _uniqueId = typeof(T).FullName ?? throw new InvalidOperationException();
        Value = _repository.GetState<T>(_uniqueId) ?? new T();
    }

    public void Dispose()
    {
        _repository.SetState<T>(_uniqueId, Value);
    }
}

public interface IStateRepository
{
    void SetState<T>(string id, T data);
    T? GetState<T>(string id) where T : class;
}

public class StateRepository : IStateRepository
{
    private readonly string _dataStoragePath = "./apps/.storage";
    private readonly JsonSerializerOptions _jsonOptions;

    public StateRepository(IOptions<AppConfigurationLocationSetting> locationSettings)
    {
        ArgumentNullException.ThrowIfNull(locationSettings.Value.ApplicationConfigurationFolder);

        _dataStoragePath = Path.GetFullPath(Path.Combine(locationSettings.Value.ApplicationConfigurationFolder, ".storage"));
        _jsonOptions = new JsonSerializerOptions();
    }

    /// <inheritdoc />
    [SuppressMessage("", "CA1031")]
    public T? GetState<T>(string id) where T : class
    {
        try
        {
            var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");

            if (!File.Exists(storageJsonFile))
                return null;

            using var jsonStream = File.OpenRead(storageJsonFile);

            return JsonSerializer.Deserialize<T>(jsonStream, _jsonOptions);
        }
        catch
        {
            // We ignore errors, we will be adding logging later see issue #403
        }
#pragma warning disable CS8603, CS8653
        return default;
#pragma warning restore CS8603, CS8653
    }

    /// <inheritdoc />
    public void SetState<T>(string id, T data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SaveInternal<T>(id, data);
    }

    private void SaveInternal<T>(string id, T data)
    {
        var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");

        if (!Directory.Exists(_dataStoragePath)) Directory.CreateDirectory(_dataStoragePath);

        using var jsonStream = File.Open(storageJsonFile, FileMode.Create, FileAccess.Write);

        JsonSerializer.Serialize(jsonStream, data);
    }
}
