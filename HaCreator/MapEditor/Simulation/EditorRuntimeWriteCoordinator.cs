using System;
using System.Threading;

namespace HaCreator.MapEditor.Simulation;

/// <summary>
/// Coordinates editor writes with an active detached preview.  Preview sources
/// are opened from the same on-disk generation as the editor, so replacing or
/// saving that generation while the preview is reading it would produce a
/// mixed session.  The editor therefore uses a fail-fast write policy until all
/// preview leases have been released.
/// </summary>
public static class EditorRuntimeWriteCoordinator
{
    private static readonly object gate = new();
    private static int activePreviews;
    private static int activeWrites;

    public static bool IsPreviewActive => Volatile.Read(ref activePreviews) != 0;
    public static bool IsWriteActive => Volatile.Read(ref activeWrites) != 0;

    /// <summary>Registers one preview generation until the returned lease is disposed.</summary>
    public static IDisposable EnterPreview()
    {
        lock (gate)
        {
            if (activeWrites != 0)
            {
                throw new InvalidOperationException(
                    "Cannot start a map preview while an editor write is in progress. Retry after the write completes.");
            }

            activePreviews++;
            return new PreviewLease();
        }
    }

    /// <summary>
    /// Enters one editor write operation atomically with the preview check. The
    /// scope must remain alive until the complete synchronous or asynchronous
    /// write has finished. A preview started after this call cannot observe a
    /// partially committed write.
    /// </summary>
    public static IDisposable EnterWrite(string operation)
    {
        lock (gate)
        {
            if (activePreviews != 0)
            {
                throw new InvalidOperationException(
                    $"Cannot {operation} while a map preview is active. Close the preview and retry.");
            }

            activeWrites++;
            return new WriteLease();
        }
    }

    /// <summary>
    /// Throws before an editor save/repack operation can change the generation
    /// observed by a running preview. Callers should report the message and ask
    /// the user to close the preview before retrying.
    /// </summary>
    public static void EnsureWriteAllowed(string operation)
    {
        lock (gate)
        {
            if (activePreviews != 0)
                throw new InvalidOperationException(
                    $"Cannot {operation} while a map preview is active. Close the preview and retry.");
        }
    }

    private sealed class PreviewLease : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            lock (gate)
            {
                activePreviews--;
            }
        }
    }

    private sealed class WriteLease : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            lock (gate)
            {
                activeWrites--;
            }
        }
    }
}
