#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio;

public readonly record struct WaveformCacheKey(string SourceHash, string DecodeFormat, int Resolution)
{
    public string FileName
    {
        get
        {
            var value = $"{SourceHash}|{DecodeFormat}|{Resolution}";
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
            return hash + ".waveform.json";
        }
    }
}

public sealed class AudioWaveformLevel
{
    public int Resolution { get; set; }
    public float[][] Minimum { get; set; } = Array.Empty<float[]>();
    public float[][] Maximum { get; set; } = Array.Empty<float[]>();
    public float[][] Rms { get; set; } = Array.Empty<float[]>();

    public int BucketCount => Minimum.Length == 0 ? 0 : Minimum[0].Length;
}

public sealed class AudioWaveformData
{
    public string SourceHash { get; set; } = string.Empty;
    public string DecodeFormat { get; set; } = string.Empty;
    public long SampleCount { get; set; }
    public int SampleRate { get; set; }
    public int ChannelCount { get; set; }
    public List<AudioWaveformLevel> Levels { get; set; } = new();

    /// <summary>
    /// Verifies that data loaded from a cache belongs to the requested key and has a
    /// structurally valid peak pyramid. Cache files are disposable, so callers should
    /// treat a failed validation as a cache miss and regenerate the data.
    /// </summary>
    public bool IsValidFor(WaveformCacheKey key)
    {
        if (!string.Equals(SourceHash ?? string.Empty, key.SourceHash ?? string.Empty,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(DecodeFormat ?? string.Empty, key.DecodeFormat ?? string.Empty,
                StringComparison.OrdinalIgnoreCase) ||
            SampleCount < 0 || SampleRate <= 0 || ChannelCount <= 0 ||
            Levels is null || Levels.Count == 0)
            return false;

        foreach (var level in Levels)
        {
            if (level is null || level.Resolution <= 0 ||
                level.Minimum is null || level.Maximum is null || level.Rms is null ||
                level.Minimum.Length != ChannelCount || level.Maximum.Length != ChannelCount ||
                level.Rms.Length != ChannelCount)
                return false;
            var expectedBuckets = (int)Math.Min(int.MaxValue,
                Math.Max(1L, (SampleCount + level.Resolution - 1L) / level.Resolution));
            for (var channel = 0; channel < ChannelCount; channel++)
            {
                if (level.Minimum[channel] is null || level.Maximum[channel] is null || level.Rms[channel] is null ||
                    level.Minimum[channel].Length != expectedBuckets ||
                    level.Maximum[channel].Length != expectedBuckets ||
                    level.Rms[channel].Length != expectedBuckets)
                    return false;
                for (var bucket = 0; bucket < expectedBuckets; bucket++)
                {
                    var minimum = level.Minimum[channel][bucket];
                    var maximum = level.Maximum[channel][bucket];
                    var rms = level.Rms[channel][bucket];
                    if (float.IsNaN(minimum) || float.IsInfinity(minimum) ||
                        float.IsNaN(maximum) || float.IsInfinity(maximum) ||
                        float.IsNaN(rms) || float.IsInfinity(rms) || rms < 0 || minimum > maximum)
                        return false;
                }
            }
        }
        return true;
    }

    public static AudioWaveformData Build(AudioBuffer buffer, string sourceHash, int resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (resolution <= 0)
            throw new ArgumentOutOfRangeException(nameof(resolution));
        var resolutions = new List<int>();
        var current = resolution;
        while (true)
        {
            resolutions.Add(current);
            if (current >= buffer.SampleCount || current > int.MaxValue / 2)
                break;
            current *= 2;
        }
        return BuildPyramid(buffer, sourceHash, resolutions, cancellationToken);
    }

    public static AudioWaveformData BuildPyramid(AudioBuffer buffer, string sourceHash,
        IEnumerable<int> resolutions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(resolutions);
        var levels = new List<AudioWaveformLevel>();
        foreach (var resolution in resolutions.Distinct().OrderBy(value => value))
        {
            if (resolution <= 0)
                throw new ArgumentOutOfRangeException(nameof(resolutions));
            var level = new AudioWaveformLevel
            {
                Resolution = resolution,
                Minimum = new float[buffer.Format.ChannelCount][],
                Maximum = new float[buffer.Format.ChannelCount][],
                Rms = new float[buffer.Format.ChannelCount][],
            };
            var buckets = (int)Math.Min(int.MaxValue, Math.Max(1L, (buffer.SampleCount + resolution - 1L) / resolution));
            for (var channel = 0; channel < buffer.Format.ChannelCount; channel++)
            {
                level.Minimum[channel] = new float[buckets];
                level.Maximum[channel] = new float[buckets];
                level.Rms[channel] = new float[buckets];
                for (var bucket = 0; bucket < buckets; bucket++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var start = bucket * (long)resolution;
                    var end = Math.Min(buffer.SampleCount, start + resolution);
                    if (end <= start)
                        continue;
                    var minimum = float.MaxValue;
                    var maximum = float.MinValue;
                    double sumSquares = 0;
                    for (var sample = start; sample < end; sample++)
                    {
                        var value = buffer.Samples[channel][(int)sample];
                        minimum = Math.Min(minimum, value);
                        maximum = Math.Max(maximum, value);
                        sumSquares += value * value;
                    }
                    level.Minimum[channel][bucket] = minimum;
                    level.Maximum[channel][bucket] = maximum;
                    level.Rms[channel][bucket] = (float)Math.Sqrt(sumSquares / (end - start));
                }
            }
            levels.Add(level);
        }
        return new AudioWaveformData
        {
            SourceHash = sourceHash ?? string.Empty,
            DecodeFormat = buffer.Format.ToString(),
            SampleCount = buffer.SampleCount,
            SampleRate = buffer.Format.SampleRate,
            ChannelCount = buffer.Format.ChannelCount,
            Levels = levels,
        };
    }
}

public interface IAudioWaveformCache
{
    ValueTask<AudioWaveformData?> TryGetAsync(WaveformCacheKey key, CancellationToken cancellationToken = default);
    ValueTask<AudioWaveformData?> GetAsync(WaveformCacheKey key, CancellationToken cancellationToken = default);
    ValueTask SetAsync(WaveformCacheKey key, AudioWaveformData data, CancellationToken cancellationToken = default);
    ValueTask<AudioWaveformData> GetOrCreateAsync(WaveformCacheKey key,
        Func<CancellationToken, ValueTask<AudioWaveformData>> factory,
        CancellationToken cancellationToken = default);
    void Clear();
}

public sealed class AudioWaveformCacheOptions
{
    public string? DirectoryPath { get; set; }
    public int MaximumEntries { get; set; } = 64;
    public long MaximumBytes { get; set; } = 128 * 1024 * 1024;

    /// <summary>Maximum aggregate size of persisted waveform files. Zero disables disk eviction.</summary>
    public long MaximumDiskBytes { get; set; } = 512 * 1024 * 1024;
}

/// <summary>Bounded LRU memory cache with optional JSON disk persistence and corruption recovery.</summary>
public class AudioWaveformCache : IAudioWaveformCache, IDisposable
{
    private readonly object gate = new();
    private readonly AudioWaveformCacheOptions options;
    private readonly Dictionary<WaveformCacheKey, CacheEntry> entries = new();
    private long bytes;
    private bool disposed;

    public AudioWaveformCache(AudioWaveformCacheOptions? options = null)
    {
        this.options = options ?? new AudioWaveformCacheOptions();
        if (this.options.MaximumEntries <= 0)
            this.options.MaximumEntries = 1;
        if (this.options.MaximumBytes <= 0)
            this.options.MaximumBytes = 1;
        if (this.options.MaximumDiskBytes < 0)
            this.options.MaximumDiskBytes = 0;
        if (!string.IsNullOrWhiteSpace(this.options.DirectoryPath))
            Directory.CreateDirectory(this.options.DirectoryPath);
    }

    public async ValueTask<AudioWaveformData?> TryGetAsync(WaveformCacheKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            EnsureNotDisposed();
            if (entries.TryGetValue(key, out var entry))
            {
                entry.LastUsedUtc = DateTime.UtcNow;
                return entry.Data;
            }
        }
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            return null;
        var path = Path.Combine(options.DirectoryPath, key.FileName);
        if (!File.Exists(path))
            return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var data = await JsonSerializer.DeserializeAsync<AudioWaveformData>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (data is null || !data.IsValidFor(key))
            {
                // A valid JSON document can still be stale or belong to another key
                // (for example after a cache directory is copied between projects).
                // Treat it exactly like corruption and regenerate on the next request.
                try { File.Delete(path); } catch { /* best-effort cache cleanup */ }
                return null;
            }
            AddMemory(key, data);
            TouchDisk(path);
            return data;
        }
        catch (Exception exception) when (exception is JsonException or IOException or NotSupportedException)
        {
            try { File.Delete(path); } catch { /* cache corruption should not fail an edit */ }
            return null;
        }
    }

    public ValueTask<AudioWaveformData?> GetAsync(WaveformCacheKey key,
        CancellationToken cancellationToken = default) => TryGetAsync(key, cancellationToken);

    public async ValueTask SetAsync(WaveformCacheKey key, AudioWaveformData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();
        AddMemory(key, data);
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            return;
        var path = Path.Combine(options.DirectoryPath, key.FileName);
        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, data, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            File.Move(temporaryPath, path, true);
            EvictDiskFiles();
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async ValueTask<AudioWaveformData> GetOrCreateAsync(WaveformCacheKey key,
        Func<CancellationToken, ValueTask<AudioWaveformData>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var cached = await TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
            return cached;
        var generated = await factory(cancellationToken).ConfigureAwait(false);
        await SetAsync(key, generated, cancellationToken).ConfigureAwait(false);
        return generated;
    }

    public void Clear()
    {
        lock (gate)
        {
            entries.Clear();
            bytes = 0;
        }
        if (!string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(options.DirectoryPath, "*.waveform.json"))
                    File.Delete(path);
            }
            catch (IOException) { /* cache cleanup is best effort */ }
            catch (UnauthorizedAccessException) { /* cache cleanup is best effort */ }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            entries.Clear();
            bytes = 0;
        }
    }

    private static void TouchDisk(string path)
    {
        try
        {
            // LastWriteTime is used for disk LRU ordering. Keep the file contents
            // immutable while making recently read files less likely to be evicted.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void EvictDiskFiles()
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath) || options.MaximumDiskBytes <= 0)
            return;
        try
        {
            var files = Directory.EnumerateFiles(options.DirectoryPath, "*.waveform.json")
                .Select(path => new FileInfo(path))
                .OrderBy(info => info.LastWriteTimeUtc)
                .ToList();
            var total = files.Sum(info => info.Length);
            foreach (var file in files)
            {
                if (total <= options.MaximumDiskBytes)
                    break;
                try
                {
                    total -= file.Length;
                    file.Delete();
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void AddMemory(WaveformCacheKey key, AudioWaveformData data)
    {
        var size = EstimateSize(data);
        lock (gate)
        {
            EnsureNotDisposed();
            if (entries.TryGetValue(key, out var existing))
                bytes -= existing.Size;
            entries[key] = new CacheEntry(data, size);
            bytes += size;
            while (entries.Count > options.MaximumEntries || bytes > options.MaximumBytes)
            {
                var oldest = entries.OrderBy(pair => pair.Value.LastUsedUtc).First();
                bytes -= oldest.Value.Size;
                entries.Remove(oldest.Key);
            }
        }
    }

    private static long EstimateSize(AudioWaveformData data)
        => data.Levels.Sum(level => level.Minimum.Sum(channel => channel.LongLength) +
            level.Maximum.Sum(channel => channel.LongLength) + level.Rms.Sum(channel => channel.LongLength)) * sizeof(float);

    private void EnsureNotDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(AudioWaveformCache));
    }

    private sealed class CacheEntry
    {
        public CacheEntry(AudioWaveformData data, long size)
        {
            Data = data;
            Size = size;
            LastUsedUtc = DateTime.UtcNow;
        }

        public AudioWaveformData Data { get; }
        public long Size { get; }
        public DateTime LastUsedUtc { get; set; }
    }
}

public sealed class InMemoryAudioWaveformCache : AudioWaveformCache
{
    public InMemoryAudioWaveformCache(int maximumEntries = 64, long maximumBytes = 128 * 1024 * 1024)
        : base(new AudioWaveformCacheOptions { MaximumEntries = maximumEntries, MaximumBytes = maximumBytes })
    {
    }
}
