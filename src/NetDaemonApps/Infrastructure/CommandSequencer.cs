namespace NetDaemonApps;

public interface ICommandSequencer
{
    Task ExecuteAsync(
        IEnumerable<Action> actions,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}

public sealed class CommandSequencer(TimeProvider timeProvider) : ICommandSequencer
{
    public async Task ExecuteAsync(
        IEnumerable<Action> actions,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        var isFirst = true;
        foreach (var action in actions)
        {
            if (!isFirst)
            {
                await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            action();
            isFirst = false;
        }
    }
}
