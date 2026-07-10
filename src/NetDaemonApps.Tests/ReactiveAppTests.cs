using FluentAssertions;
using NSubstitute;

namespace NetDaemonApps.Tests;

public sealed class ReactiveAppTests
{
    [Fact]
    public async Task InitializeOnce_ShouldDisposePartialResources_BeforeRetry()
    {
        var firstResource = Substitute.For<IDisposable>();
        var secondResource = Substitute.For<IDisposable>();
        using var app = new FailingOnceApp([firstResource, secondResource]);

        var firstAttempt = () => app.InitializeAsync(CancellationToken.None);

        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();
        firstResource.Received(1).Dispose();

        await app.InitializeAsync(CancellationToken.None);
        secondResource.DidNotReceive().Dispose();

        app.Dispose();
        secondResource.Received(1).Dispose();
    }

    private sealed class FailingOnceApp(IReadOnlyList<IDisposable> resources) : ReactiveApp
    {
        private int _attempt;

        public Task InitializeAsync(CancellationToken cancellationToken) => InitializeOnce(() =>
        {
            Track(resources[_attempt]);
            if (_attempt++ == 0)
            {
                throw new InvalidOperationException("First initialization attempt failed.");
            }
        }, cancellationToken);
    }
}
