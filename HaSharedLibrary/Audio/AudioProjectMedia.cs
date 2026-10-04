#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio;

public sealed class AudioMediaCollectionResult
{
    public int FilesCopied { get; internal set; }
    public List<AudioSourceReference> UpdatedReferences { get; } = new();
    public List<AudioDiagnostic> Diagnostics { get; } = new();
    public bool Succeeded => Diagnostics.All(diagnostic => !diagnostic.IsError);
}

/// <summary>Copies external clip media into a project media folder and rewrites references atomically per file.</summary>
public sealed class AudioProjectMediaCollector
{
    public async Task<AudioMediaCollectionResult> CollectAsync(AudioProject project, string projectPath,
        string mediaDirectory = "media", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaDirectory);
        var result = new AudioMediaCollectionResult();
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? Directory.GetCurrentDirectory();
        var mediaRoot = Path.GetFullPath(Path.Combine(projectDirectory, mediaDirectory));
        Directory.CreateDirectory(mediaRoot);
        var copiedByHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var clip in project.Tracks.SelectMany(track => track.Clips))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = clip.SourceReference;
            if (source is null || !source.IsExternal || string.IsNullOrWhiteSpace(source.ExternalPath))
                continue;
            var sourcePath = source.ResolveExternalPath(projectDirectory);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                result.Diagnostics.Add(new AudioDiagnostic(AudioDiagnosticCode.MissingSource,
                    $"External media '{source.ExternalPath}' could not be collected.", true));
                continue;
            }
            var hash = await ComputeHashAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            if (!copiedByHash.TryGetValue(hash, out var destinationPath))
            {
                var fileName = SanitizeFileName(Path.GetFileName(sourcePath));
                destinationPath = Path.Combine(mediaRoot, hash[..Math.Min(16, hash.Length)] + "_" + fileName);
                if (!File.Exists(destinationPath))
                {
                    var temporaryPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                            FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                        }
                        File.Move(temporaryPath, destinationPath, overwrite: false);
                    }
                    finally
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    result.FilesCopied++;
                }
                copiedByHash[hash] = destinationPath;
            }

            source.SourceKind = AudioSourceKind.ProjectMedia;
            source.ExternalPath = Path.GetRelativePath(projectDirectory, destinationPath).Replace(Path.DirectorySeparatorChar, '/');
            source.ContentHash = hash;
            source.SourceId ??= hash;
            result.UpdatedReferences.Add(source);
        }
        return result;
    }

    public static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "audio.bin";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
        return new string(chars);
    }
}

public enum AudioSourceLinkStatus
{
    Missing,
    Valid,
    HashMismatch,
}

public sealed class AudioSourceLinkResult
{
    public AudioSourceLinkStatus Status { get; init; }
    public string? ResolvedPath { get; init; }
    public string? ActualHash { get; init; }
    public string? Message { get; init; }
}

public static class AudioSourceRelinker
{
    public static async Task<AudioSourceLinkResult> CheckAsync(AudioSourceReference source,
        string? projectDirectory = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var path = source.ResolveExternalPath(projectDirectory);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new AudioSourceLinkResult { Status = AudioSourceLinkStatus.Missing, ResolvedPath = path,
                Message = $"The source '{source.ExternalPath}' is missing." };
        var hash = await AudioProjectMediaCollector.ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(source.ContentHash) &&
            !string.Equals(hash, source.ContentHash, StringComparison.OrdinalIgnoreCase))
            return new AudioSourceLinkResult { Status = AudioSourceLinkStatus.HashMismatch, ResolvedPath = path,
                ActualHash = hash, Message = "The source content hash does not match the project reference." };
        return new AudioSourceLinkResult { Status = AudioSourceLinkStatus.Valid, ResolvedPath = path, ActualHash = hash };
    }

    public static async Task<bool> RelinkAsync(AudioSourceReference source, string candidatePath,
        string? projectDirectory = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        if (!File.Exists(candidatePath))
            return false;
        var hash = await AudioProjectMediaCollector.ComputeHashAsync(candidatePath, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(source.ContentHash) &&
            !string.Equals(hash, source.ContentHash, StringComparison.OrdinalIgnoreCase))
            return false;
        source.ExternalPath = Path.IsPathRooted(candidatePath) || string.IsNullOrWhiteSpace(projectDirectory)
            ? Path.GetFullPath(candidatePath)
            : Path.GetRelativePath(projectDirectory, candidatePath).Replace(Path.DirectorySeparatorChar, '/');
        source.SourceKind = AudioSourceKind.ExternalFile;
        source.ContentHash = hash;
        return true;
    }
}
