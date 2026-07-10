using System.Reactive.Disposables;

namespace NetDaemonApps;

/// <summary>
/// Owns the lifetime of subscriptions and schedules created by an automation app.
/// </summary>
public abstract class ReactiveApp : IDisposable
{
    private readonly CompositeDisposable _resources = [];
    private CompositeDisposable? _initializingResources;
    private int _initialized;

    protected Task InitializeOnce(Action initialize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return Task.CompletedTask;
        }

        var initializingResources = new CompositeDisposable();
        _initializingResources = initializingResources;

        try
        {
            initialize();
            _resources.Add(initializingResources);
            return Task.CompletedTask;
        }
        catch
        {
            initializingResources.Dispose();
            Volatile.Write(ref _initialized, 0);
            throw;
        }
        finally
        {
            _initializingResources = null;
        }
    }

    protected T Track<T>(T resource)
        where T : IDisposable
    {
        (_initializingResources ?? _resources).Add(resource);
        return resource;
    }

    protected void SubscribeLogged<T>(
        IObservable<T> source,
        ILogger logger,
        string operation,
        Action<T> onNext)
    {
        Track(Recover(source, logger, operation).Subscribe(
            value =>
            {
                try
                {
                    onNext(value);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Automation operation {Operation} failed", operation);
                }
            }));
    }

    protected void SubscribeLoggedAsync<T>(
        IObservable<T> source,
        ILogger logger,
        string operation,
        Func<T, Task> onNext)
    {
        var operations = Recover(source, logger, operation)
            .Select(value => Observable.FromAsync(async cancellationToken =>
            {
                try
                {
                    await onNext(value).WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when the subscription is disposed.
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Automation operation {Operation} failed", operation);
                }
            }))
            .Concat();

        Track(operations.Subscribe());
    }

    protected static async Task ExecuteLoggedAsync(
        ICommandSequencer commandSequencer,
        IEnumerable<Action> actions,
        TimeSpan delay,
        ILogger logger,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await commandSequencer.ExecuteAsync(actions, delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during app shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Automation operation {Operation} failed", operation);
        }
    }

    private static IObservable<T> Recover<T>(IObservable<T> source, ILogger logger, string operation)
    {
        IObservable<T> RecoverAfterFailure() => source.Catch<T, Exception>(exception =>
        {
            logger.LogError(
                exception,
                "Automation stream {Operation} terminated; subscribing again in five seconds",
                operation);
            return Observable.Timer(TimeSpan.FromSeconds(5)).SelectMany(_ => RecoverAfterFailure());
        });

        return RecoverAfterFailure();
    }

    public void Dispose()
    {
        _resources.Dispose();
        GC.SuppressFinalize(this);
    }
}
