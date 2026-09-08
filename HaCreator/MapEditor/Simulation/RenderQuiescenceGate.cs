using System;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Simulation
{
    /// <summary>
    /// Prevents new frames while asynchronously waiting for a frame already in
    /// progress. The UI must remain free to service renderer dispatcher requests.
    /// </summary>
    public sealed class RenderQuiescenceGate
    {
        private readonly object gate = new();
        private bool paused;
        private int frames;
        private TaskCompletionSource quiescent;

        public bool TryEnterFrame()
        {
            lock (gate)
            {
                if (paused)
                    return false;
                frames++;
                return true;
            }
        }

        public void ExitFrame()
        {
            lock (gate)
            {
                if (frames == 0)
                    throw new InvalidOperationException("No renderer frame is active.");
                frames--;
                if (frames == 0)
                    quiescent?.TrySetResult();
            }
        }

        public Task PauseAsync()
        {
            lock (gate)
            {
                paused = true;
                if (frames == 0)
                    return Task.CompletedTask;
                quiescent ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                return quiescent.Task;
            }
        }

        public void Resume()
        {
            lock (gate)
            {
                if (frames != 0)
                    throw new InvalidOperationException("Await renderer quiescence before resuming.");
                quiescent = null;
                paused = false;
            }
        }
    }
}
