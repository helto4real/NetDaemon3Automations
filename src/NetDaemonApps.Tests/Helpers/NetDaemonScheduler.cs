using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;

namespace NetDaemonApps.Tests.Helpers;

/// <summary>
///     using original until we have made it public
/// </summary>
public sealed class NetDaemonScheduler : INetDaemonScheduler, IDisposable
{
    private readonly CancellationTokenSource _cancelTimers;

    private readonly ILogger _logger;
    private readonly IScheduler _reactiveScheduler;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="logger">Injected logger</param>
    /// <param name="reactiveScheduler">Used for unit testing the scheduler</param>
    public NetDaemonScheduler(ILogger<NetDaemonScheduler>? logger = null, IScheduler? reactiveScheduler = null)
    {
        _cancelTimers = new CancellationTokenSource();
        _logger = logger ?? DefaultLoggerFactory.CreateLogger<NetDaemonScheduler>();
        _reactiveScheduler = reactiveScheduler ?? TaskPoolScheduler.Default;
    }

    private static ILoggerFactory DefaultLoggerFactory => LoggerFactory.Create(builder =>
    {
        builder
            .ClearProviders()
            .AddConsole();
    });

    /// <summary>
    ///     Implements Dispose pattern
    /// </summary>
    public void Dispose()
    {
        _cancelTimers.Cancel();
        _cancelTimers.Dispose();
    }

    /// <inheritdoc />
    public IDisposable RunEvery(TimeSpan timespan, Action action)
    {
        var result = new DisposableTimer(_cancelTimers.Token);

        Observable.Interval(timespan, _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                _ => _logger.LogTrace("Exiting timer using trigger with span {Span}",
                    timespan)
                , result.Token);

        return result;
    }

    /// <inheritdoc />
    public IDisposable RunEvery(TimeSpan period, DateTimeOffset startTime, Action action)
    {
        var result = new DisposableTimer(_cancelTimers.Token);

        Observable.Timer(
                startTime,
                period,
                _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                () => _logger.LogTrace("Exiting timer that was scheduled at {StartTime} and every {Period}",
                    startTime, period),
                result.Token
            );

        return result;
    }

    /// <inheritdoc />
    public IDisposable RunIn(TimeSpan timespan, Action action)
    {
        var result = new DisposableTimer(_cancelTimers.Token);
        Observable.Timer(timespan, _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                () => _logger.LogTrace("Exiting scheduled run at {Timespan}", timespan)
                , result.Token);
        return result;
    }

    /// <inheritdoc />
    public IDisposable RunAt(DateTimeOffset timeOffset, Action action)
    {
        var result = new DisposableTimer(_cancelTimers.Token);

        Observable.Timer(
                timeOffset,
                _reactiveScheduler)
            .Subscribe(
                _ => RunAction(action),
                () => _logger.LogTrace("Exiting timer that was scheduled at {TimeOffset}",
                    timeOffset),
                result.Token
            );

        return result;
    }

    /// <inheritdoc />
    public DateTimeOffset Now => _reactiveScheduler.Now;

    [SuppressMessage("", "CA1031")]
    private void RunAction(Action action)
    {
        try
        {
            action();
        }
        catch (OperationCanceledException)
        {
            // Do nothing
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in scheduled timer!");
        }
    }
}