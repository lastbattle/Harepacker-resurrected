using HaCreator.MapSimulator.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Simulation
{
    /// <summary>
    /// Owns one editor preview. Call on the editor synchronization context: pause,
    /// restoration and the returned task's continuation stay on that context.
    /// </summary>
    public sealed class EditorPreviewController
    {
        private int running;
        private GameSessionHandle activeSession;
        private TaskCompletionSource<GameSessionResult> lifetimeCompletion;
        private bool stopRequested;

        public bool IsRunning => Volatile.Read(ref running) != 0;

        public Task<GameSessionResult> RunAsync(
            Func<IGameSession> factory, Action pauseRendering, Action restoreRendering)
        {
            ArgumentNullException.ThrowIfNull(pauseRendering);
            return RunAsync(factory, () =>
            {
                pauseRendering();
                return Task.CompletedTask;
            }, restoreRendering);
        }

        public async Task<GameSessionResult> RunAsync(
            Func<IGameSession> factory, Func<Task> pauseRendering, Action restoreRendering)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(pauseRendering);
            ArgumentNullException.ThrowIfNull(restoreRendering);
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
                throw new InvalidOperationException("An editor preview is already active.");
            lifetimeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            stopRequested = false;

            GameSessionResult result = null;
            try
            {
                await pauseRendering();
                if (stopRequested)
                {
                    result = new GameSessionResult(GameSessionOutcome.Cancelled);
                }
                else
                {
                    if (!GameSessionHost.TryStart(factory, out activeSession))
                        throw new InvalidOperationException("Another game session is already active.");
                    result = await activeSession.Completion;
                }
            }
            catch (Exception error)
            {
                result = new GameSessionResult(GameSessionOutcome.Failed, error);
            }
            finally
            {
                try
                {
                    restoreRendering();
                }
                catch (Exception error)
                {
                    result = new GameSessionResult(GameSessionOutcome.Failed,
                        result?.Error == null ? error : new AggregateException(result.Error, error));
                }
                activeSession = null;
                Volatile.Write(ref running, 0);
                lifetimeCompletion.SetResult(result);
            }
            return result;
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;
            Task<GameSessionResult> lifetime = lifetimeCompletion.Task;
            stopRequested = true;
            activeSession?.RequestStop();
            await lifetime;
        }
    }
}
