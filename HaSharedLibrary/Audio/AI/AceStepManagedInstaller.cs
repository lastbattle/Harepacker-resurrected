#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HaSharedLibrary.Configuration;

namespace HaSharedLibrary.Audio.AI;

/// <summary>
/// Installs the upstream ACE-Step portable source environment in the user's
/// local application data. Model weights remain managed by ACE-Step and are
/// downloaded by its first API startup.
/// </summary>
public sealed class AceStepManagedInstaller
{
    private const string RepositoryZip = "https://github.com/ACE-Step/ACE-Step-1.5/archive/refs/heads/main.zip";
    private const string UvZip = "https://github.com/astral-sh/uv/releases/latest/download/uv-x86_64-pc-windows-msvc.zip";
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(30) };

    public string InstallRoot => UserDataPaths.AceStepInstallDirectory;

    public string RepositoryRoot => Path.Combine(InstallRoot, "repository");
    public string UvExecutable => Path.Combine(InstallRoot, "uv.exe");
    public bool IsInstalled => File.Exists(UvExecutable) && File.Exists(Path.Combine(RepositoryRoot, "pyproject.toml"));

    public async Task<AudioAiSidecar> InstallAndStartAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        MigrateLegacyInstallIfNeeded(progress);
        Directory.CreateDirectory(InstallRoot);
        if (!File.Exists(UvExecutable))
        {
            progress?.Report("Downloading uv runtime…");
            string archive = await DownloadAsync(UvZip, cancellationToken);
            try
            {
                using var zip = ZipFile.OpenRead(archive);
                var entry = zip.Entries.FirstOrDefault(item => item.Name.Equals("uv.exe", StringComparison.OrdinalIgnoreCase));
                if (entry is null) throw new InvalidDataException("The uv package did not contain uv.exe.");
                entry.ExtractToFile(UvExecutable, overwrite: true);
            }
            finally { TryDeleteFile(archive); }
        }

        if (!IsInstalled)
        {
            progress?.Report("Downloading ACE-Step runtime…");
            string archive = await DownloadAsync(RepositoryZip, cancellationToken);
            string staging = Path.Combine(Path.GetTempPath(), "harepacker-acestep-" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipFile.ExtractToDirectory(archive, staging);
                string source = Directory.GetDirectories(staging).FirstOrDefault(directory =>
                    File.Exists(Path.Combine(directory, "pyproject.toml")))
                    ?? throw new InvalidDataException("The ACE-Step package did not contain pyproject.toml.");
                Directory.CreateDirectory(RepositoryRoot);
                foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(source, file);
                    string destination = Path.Combine(RepositoryRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination, overwrite: true);
                }
            }
            finally { TryDeleteFile(archive); TryDeleteDirectory(staging); }
        }

        progress?.Report("Installing Python dependencies (first run can take a while)…");
        await RunProcessAsync(UvExecutable, "sync", RepositoryRoot, progress, cancellationToken);
        progress?.Report("Starting ACE-Step API; model weights will download on first use…");
        return await AudioAiSidecar.StartAsync(UvExecutable, "run acestep-api --no-init", 8765, RepositoryRoot,
            TimeSpan.FromMinutes(2), new Dictionary<string, string>
            {
                ["ACESTEP_API_HOST"] = "127.0.0.1",
                ["ACESTEP_API_PORT"] = "8765",
            }, cancellationToken);
    }

    private async Task<string> DownloadAsync(string uri, CancellationToken cancellationToken)
    {
        string destination = Path.Combine(Path.GetTempPath(), "harepacker-audio-ai-" + Guid.NewGuid().ToString("N") + ".download");
        await using Stream source = await client.GetStreamAsync(uri, cancellationToken);
        await using FileStream target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
        return destination;
    }

    private static async Task RunProcessAsync(string executable, string arguments, string workingDirectory,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("uv could not be started.");
        process.OutputDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) progress?.Report(args.Data); };
        process.ErrorDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) progress?.Report(args.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"ACE-Step dependency setup failed (exit code {process.ExitCode}).");
    }

    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { } }

    private void MigrateLegacyInstallIfNeeded(IProgress<string>? progress)
    {
        string legacyRoot = UserDataPaths.GetLegacyLocalPath(
            "HaCreator", "AudioAI", "ACE-Step-1.5");
        if (Directory.Exists(InstallRoot) || !Directory.Exists(legacyRoot)) return;
        progress?.Report("Moving the existing ACE-Step model cache to the shared Harepacker folder…");
        Directory.CreateDirectory(Path.GetDirectoryName(InstallRoot)!);
        Directory.Move(legacyRoot, InstallRoot);
    }
}
