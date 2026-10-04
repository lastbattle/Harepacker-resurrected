using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HaCreator.MapSimulator.Contracts
{
    /// <summary>A game window whose entire lifetime belongs to its hosting thread.</summary>
    public interface IGameSession : IDisposable
    {
        void Run(CancellationToken cancellationToken);
    }

    public enum GameSessionOutcome
    {
        Closed,
        Cancelled,
        Failed
    }

    public sealed record GameSessionResult(GameSessionOutcome Outcome, Exception Error = null);

    /// <summary>
    /// Hosts the current single-session runtime on an STA thread. The completion task
    /// resolves only after disposal, so hosts can safely release assets or close afterward.
    /// No callback is invoked on the caller's UI thread by this class.
    /// </summary>
    public static class GameSessionHost
    {
        private static int activeSession;

        public static bool TryStart(Func<IGameSession> factory, out GameSessionHandle handle)
        {
            ArgumentNullException.ThrowIfNull(factory);
            handle = null;
            if (Interlocked.CompareExchange(ref activeSession, 1, 0) != 0)
                return false;

            var sessionHandle = new GameSessionHandle();
            handle = sessionHandle;
            try
            {
                var thread = new Thread(() => Run(factory, sessionHandle))
                {
                    Name = "Maple game session",
                    // The host must await completion before exiting or releasing its source.
                    IsBackground = false
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch (Exception error)
            {
                Interlocked.Exchange(ref activeSession, 0);
                sessionHandle.Complete(error);
            }
            return true;
        }

        private static void Run(Func<IGameSession> factory, GameSessionHandle handle)
        {
            IGameSession session = null;
            Exception failure = null;
            try
            {
                handle.Cancellation.ThrowIfCancellationRequested();
                session = factory() ?? throw new InvalidOperationException("The game session factory returned null.");
                handle.Cancellation.ThrowIfCancellationRequested();
                session.Run(handle.Cancellation);
            }
            catch (OperationCanceledException) when (handle.Cancellation.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                try
                {
                    session?.Dispose();
                }
                catch (Exception error)
                {
                    failure = failure == null ? error : new AggregateException(failure, error);
                }
                try
                {
                    // WPF bitmap/text paths can lazily create a dispatcher and its
                    // native media context on this dedicated STA. Shut down only an
                    // existing dispatcher so repeated sessions do not retain those
                    // native worker threads after the game thread exits.
                    Dispatcher dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
                    if (dispatcher != null && !dispatcher.HasShutdownStarted)
                    {
                        dispatcher.InvokeShutdown();
                    }
                }
                catch (Exception error)
                {
                    failure = failure == null ? error : new AggregateException(failure, error);
                }
                // Release the process guard before signaling completion so a host can relaunch.
                Interlocked.Exchange(ref activeSession, 0);
                handle.Complete(failure);
            }
        }
    }

    public sealed class GameSessionHandle
    {
        private readonly object gate = new();
        private readonly CancellationTokenSource cancellation = new();
        private readonly TaskCompletionSource<GameSessionResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool completed;
        private Exception cancellationFailure;

        internal GameSessionHandle() { }

        internal CancellationToken Cancellation => cancellation.Token;
        public Task<GameSessionResult> Completion => completion.Task;

        public void RequestStop()
        {
            lock (gate)
            {
                if (completed || cancellation.IsCancellationRequested)
                    return;
                try
                {
                    cancellation.Cancel();
                }
                catch (Exception error)
                {
                    // A faulty cancellation callback must not strand the editor shutdown path.
                    cancellationFailure = error;
                }
            }
        }

        internal void Complete(Exception error)
        {
            lock (gate)
            {
                if (completed)
                    return;
                completed = true;
                if (cancellationFailure != null)
                    error = error == null ? cancellationFailure : new AggregateException(error, cancellationFailure);
                var outcome = error != null ? GameSessionOutcome.Failed
                    : cancellation.IsCancellationRequested ? GameSessionOutcome.Cancelled
                    : GameSessionOutcome.Closed;
                cancellation.Dispose();
                completion.SetResult(new GameSessionResult(outcome, error));
            }
        }
    }
}
