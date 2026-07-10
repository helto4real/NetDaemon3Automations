using FluentAssertions;

namespace NetDaemonApps.Tests;

public sealed class CommandSequencerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPreserveCommandOrder()
    {
        var calls = new List<int>();
        var sequencer = new CommandSequencer(TimeProvider.System);

        await sequencer.ExecuteAsync(
            [() => calls.Add(1), () => calls.Add(2), () => calls.Add(3)],
            TimeSpan.Zero,
            CancellationToken.None);

        calls.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHonorCancellationBeforeFirstCommand()
    {
        var sequencer = new CommandSequencer(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => sequencer.ExecuteAsync([() => { }], TimeSpan.Zero, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
