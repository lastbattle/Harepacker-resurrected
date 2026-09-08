using HaCreator.MapEditor.Simulation;
using HaCreator.MapSimulator.Contracts;
using System.Windows.Threading;

namespace UnitTest_MapSimulator;

public class GameSessionHostTests
{
    [Fact]
    public async Task ControllerRestoresAfterPreparationFailureWithoutConstructingGame()
    {
        var controller = new EditorPreviewController();
        bool restored = false;
        bool constructed = false;
        var error = new InvalidOperationException("snapshot failed");
        var result = await controller.RunAsync(
            () => { constructed = true; return new FakeSession(_ => { }); },
            (Action)(() => throw error),
            () => restored = true);
        Assert.Same(error, result.Error);
        Assert.True(restored);
        Assert.False(constructed);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public async Task StopDuringRendererPauseWaitsForRestorationWithoutConstructingGame()
    {
        var controller = new EditorPreviewController();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool constructed = false, restored = false;
        var preview = controller.RunAsync(() =>
        {
            constructed = true;
            return new FakeSession(_ => { });
        }, () => paused.Task, () => restored = true);
        var stopping = controller.StopAsync();
        Assert.False(stopping.IsCompleted);
        paused.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(restored);
        Assert.False(constructed);
        Assert.Equal(GameSessionOutcome.Cancelled, (await preview).Outcome);
    }

    [Fact]
    public async Task RenderPauseWaitsForExistingFrameAndRejectsNewFrames()
    {
        var gate = new RenderQuiescenceGate();
        Assert.True(gate.TryEnterFrame());
        Task pause = gate.PauseAsync();
        Assert.False(pause.IsCompleted);
        Assert.False(gate.TryEnterFrame());
        gate.ExitFrame();
        await pause.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(gate.TryEnterFrame());
        gate.Resume();
        Assert.True(gate.TryEnterFrame());
        gate.ExitFrame();
        await gate.PauseAsync(); // The next pause must not reuse a stale completion.
        gate.Resume();
    }

    [Fact]
    public async Task ControllerRestoresOnUiContextBeforeStopCompletes()
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiThread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    int uiThreadId = Environment.CurrentManagedThreadId;
                    bool restored = false;
                    var controller = new EditorPreviewController();
                    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var preview = controller.RunAsync(() => new FakeSession(token =>
                    {
                        entered.SetResult();
                        Assert.True(token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)));
                    }), () => { }, () =>
                    {
                        Assert.Equal(uiThreadId, Environment.CurrentManagedThreadId);
                        restored = true;
                    });
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await controller.StopAsync();
                    Assert.True(restored);
                    Assert.False(controller.IsRunning);
                    Assert.Equal(GameSessionOutcome.Cancelled, (await preview).Outcome);
                    finished.SetResult();
                }
                catch (Exception error)
                {
                    finished.SetException(error);
                }
                finally
                {
                    dispatcher.InvokeShutdown();
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(15));
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
