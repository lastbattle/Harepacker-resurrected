using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MapleGame.Client;

/// <summary>Keeps the desktop host quiet while retaining command-line diagnostics.</summary>
internal static class ClientPresentation
{
    private const int StandardOutput = -11;
    private const int StandardError = -12;

    internal static void Initialize()
    {
        // A redirected stream is already usable and must not be replaced by a console.
        if (!HasStream(StandardOutput) && !HasStream(StandardError) && AttachConsole(uint.MaxValue))
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    internal static void ShowHelp(string text)
    {
        if (HasStream(StandardOutput)) Console.WriteLine(text);
        else MessageBox.Show(text, "MapleGame Client", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    internal static void ReportFailure(Exception error)
    {
        if (HasStream(StandardError)) Console.Error.WriteLine(error.ToString());
        else MessageBox.Show(error.ToString(), "MapleGame Client — startup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static bool HasStream(int stream)
    {
        IntPtr handle = GetStdHandle(stream);
        return handle != IntPtr.Zero && handle != new IntPtr(-1) && GetFileType(handle) != 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);
}
