using HaCreator.MapSimulator.Contracts;

namespace UnitTest_MapSimulator;

[Collection("Game session host")]
public class GameSessionHostTests
{
    [Fact]
    public async Task SessionRunsAndDisposesOnOneStaThreadBeforeCompletion()
    {
        int runThread = 0, disposeThread = 0;
        ApartmentState apartment = ApartmentState.Unknown;
        var session = new FakeSession(_ =>
        {
            runThread = Environment.CurrentManagedThreadId;
            apartment = Thread.CurrentThread.GetApartmentState();
        }, () => disposeThread = Environment.CurrentManagedThreadId);

        Assert.True(GameSessionHost.TryStart(() => session, out var handle));
        var result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
        Assert.Equal(ApartmentState.STA, apartment);
        Assert.Equal(runThread, disposeThread);
        Assert.Equal(1, session.DisposeCount);
        handle.RequestStop(); // Late close requests are harmless.
    }

    [Fact]
    public async Task DuplicateSessionIsRejectedAndStopReleasesGuard()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeSession(token =>
        {
            entered.SetResult();
            Assert.True(token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)));
        });
        Assert.True(GameSessionHost.TryStart(() => session, out var handle));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(GameSessionHost.TryStart(() => throw new Exception("Must not construct"), out var rejected));
            Assert.Null(rejected);
        }
        finally
        {
            handle.RequestStop();
        }
        Assert.Equal(GameSessionOutcome.Cancelled,
            (await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10))).Outcome);
        Assert.Equal(1, session.DisposeCount);

        Assert.True(GameSessionHost.TryStart(() => new FakeSession(_ => { }), out var next));
        Assert.Equal(GameSessionOutcome.Closed,
            (await next.Completion.WaitAsync(TimeSpan.FromSeconds(10))).Outcome);
    }

    [Fact]
    public async Task ConstructorFailureCompletesAndAllowsRelaunch()
    {
        var error = new InvalidOperationException("constructor failed");
        Assert.True(GameSessionHost.TryStart(() => throw error, out var handle));
        var result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(GameSessionOutcome.Failed, result.Outcome);
        Assert.Same(error, result.Error);
        Assert.True(GameSessionHost.TryStart(() => new FakeSession(_ => { }), out var next));
        await next.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAndDisposeFailuresAreBothPreserved()
    {
        var runError = new InvalidOperationException("run failed");
        var disposeError = new InvalidOperationException("dispose failed");
        var session = new FakeSession(_ => throw runError, () => throw disposeError);
        Assert.True(GameSessionHost.TryStart(() => session, out var handle));
        var result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var aggregate = Assert.IsType<AggregateException>(result.Error);
        Assert.Equal(new[] { runError, disposeError }, aggregate.InnerExceptions);
        Assert.Equal(GameSessionOutcome.Failed, result.Outcome);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public async Task UnrequestedCancellationExceptionIsFailure()
    {
        Assert.True(GameSessionHost.TryStart(
            () => new FakeSession(_ => throw new OperationCanceledException()), out var handle));
        Assert.Equal(GameSessionOutcome.Failed,
            (await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10))).Outcome);
    }

    private sealed class FakeSession(Action<CancellationToken> run, Action? dispose = null) : IGameSession
    {
        public int DisposeCount { get; private set; }
        public void Run(CancellationToken cancellationToken) => run(cancellationToken);
        public void Dispose()
        {
            DisposeCount++;
            dispose?.Invoke();
        }
    }
}
