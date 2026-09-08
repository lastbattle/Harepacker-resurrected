using System;
using System.IO;
using SharpDX;

namespace UnitTest_MapleGame;

/// <summary>Opt-in diagnostic; deliberately preserves SharpDX finalizer release policy.</summary>
internal sealed class SharpDxLeakDiagnostic : IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;
    private readonly bool previousTracking;
    private readonly bool previousThreadStaticTracking;
    private readonly bool previousWarnings;
    private readonly Action<string> previousLog;
    private readonly Func<string> previousStackTraceProvider;
    private bool disposed;

    private SharpDxLeakDiagnostic(string path)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        writer = new StreamWriter(fullPath, append: false) { AutoFlush = true };
        previousTracking = Configuration.EnableObjectTracking;
        previousThreadStaticTracking = Configuration.UseThreadStaticObjectTracking;
        previousWarnings = Configuration.EnableTrackingReleaseOnFinalizer;
        previousLog = ComObject.LogMemoryLeakWarning;
        previousStackTraceProvider = SharpDX.Diagnostics.ObjectTracker.StackTraceProvider;
        writer.WriteLine($"SharpDX allocation/finalizer diagnostic started {DateTime.UtcNow:O}; EnableReleaseOnFinalizer={Configuration.EnableReleaseOnFinalizer}");
        SharpDX.Diagnostics.ObjectTracker.StackTraceProvider = () => new System.Diagnostics.StackTrace(true).ToString();
        Configuration.UseThreadStaticObjectTracking = false;
        Configuration.EnableObjectTracking = true;
        Configuration.EnableTrackingReleaseOnFinalizer = true;
        ComObject.LogMemoryLeakWarning = WriteWarning;
    }

    public static SharpDxLeakDiagnostic? StartFromEnvironment()
    {
        string? path = Environment.GetEnvironmentVariable("MAPLEGAME_SHARPDX_LEAK_LOG");
        return string.IsNullOrWhiteSpace(path) ? null : new SharpDxLeakDiagnostic(path);
    }

    private void WriteWarning(string warning)
    {
        // Called on the finalizer thread: diagnostics must never terminate the process.
        try
        {
            lock (gate)
            {
                if (!disposed) writer.WriteLine(warning);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            ComObject.LogMemoryLeakWarning = previousLog;
            SharpDX.Diagnostics.ObjectTracker.StackTraceProvider = previousStackTraceProvider;
            Configuration.EnableTrackingReleaseOnFinalizer = previousWarnings;
            Configuration.EnableObjectTracking = previousTracking;
            Configuration.UseThreadStaticObjectTracking = previousThreadStaticTracking;
            writer.Dispose();
        }
    }
}
