#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

/// <summary>Owns only a sidecar process launched by this instance; user-managed endpoints are never terminated.</summary>
public sealed class AudioAiSidecar : IAsyncDisposable
{
    private readonly Process? process;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };
    public string Endpoint { get; }
    public string BearerToken { get; }
    public bool OwnsProcess => process is not null;

    private AudioAiSidecar(Process? process, string endpoint, string token) { this.process = process; Endpoint = endpoint; BearerToken = token; }

    public static async Task<AudioAiSidecar> StartAsync(string executable, string arguments, int port,
        string workingDirectory, TimeSpan startupTimeout, IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executable)) throw new FileNotFoundException("Audio AI sidecar executable was not found.", executable);
        Directory.CreateDirectory(workingDirectory);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var info = new ProcessStartInfo(executable, arguments) { WorkingDirectory = workingDirectory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["HAREPACKER_AUDIO_AI_TOKEN"] = token;
        info.Environment["HAREPACKER_AUDIO_AI_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (environment is not null)
            foreach (var pair in environment)
                info.Environment[pair.Key] = pair.Value;
        var process = Process.Start(info) ?? throw new InvalidOperationException("The Audio AI sidecar could not be started.");
        // Always drain redirected streams. ACE-Step is verbose during model
        // discovery/download and will block when an unread pipe buffer fills.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var sidecar = new AudioAiSidecar(process, $"http://127.0.0.1:{port}", token);
        var deadline = DateTime.UtcNow + startupTimeout;
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidOperationException("The Audio AI sidecar exited before becoming healthy.");
                try { using var response = await sidecar.client.GetAsync(sidecar.Endpoint + "/health", cancellationToken).ConfigureAwait(false); if (response.IsSuccessStatusCode) return sidecar; }
                catch (HttpRequestException) { }
                // A cold Python/uv startup can exceed the short probe timeout;
                // retry until startupTimeout unless the caller explicitly cancels.
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException("The Audio AI sidecar did not become healthy before the startup timeout.");
        }
        catch { await sidecar.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (process is null)
        {
            client.Dispose();
            return;
        }
        try
        {
            if (!process.HasExited)
            {
                // ACE-Step may keep several model workers alive. Give the API
                // a chance to unload them and flush its cache before falling
                // back to process termination. Older builds do not expose
                // this endpoint, so a missing/failing request is intentional.
                using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
                try
                {
                    using var response = await client.PostAsync(Endpoint + "/shutdown", content: null, shutdown.Token).ConfigureAwait(false);
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }

                // Also handle providers that expose a desktop window rather
                // than the optional HTTP endpoint.
                try { if (process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow(); }
                catch (InvalidOperationException) { }

                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await process.WaitForExitAsync(wait.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (wait.IsCancellationRequested) { }
            }
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            process.Dispose();
            client.Dispose();
        }
    }
}
