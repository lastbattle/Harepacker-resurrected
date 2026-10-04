using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HaSharedLibrary.Audio;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.Audio;

public enum AudioAssetDifferenceKind { Added, Removed, Changed, Unchanged }

public sealed record AudioAssetDifference(
    AudioAssetDifferenceKind Kind,
    string CanonicalPath,
    AudioAssetEntry Active,
    AudioAssetEntry Comparison,
    IReadOnlyList<string> Differences)
{
    /// <summary>True when copying the comparison payload would overwrite an existing active asset.</summary>
    public bool CopyConflict { get; init; }
    public string ActiveEncodedHash { get; init; }
    public string ComparisonEncodedHash { get; init; }
    public string ActiveDecodedHash { get; init; }
    public string ComparisonDecodedHash { get; init; }
}

/// <summary>Compares two independently backed Sound catalogs without changing HaCreator's global data source.</summary>
public sealed class AudioVersionComparisonService
{
    public Task<IReadOnlyList<AudioAssetDifference>> CompareAsync(
        IAudioAssetCatalog active,
        IAudioAssetCatalog comparison,
        CancellationToken cancellationToken)
        => CompareAsync(active, comparison, includeUnchanged: false, compareHashes: true,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AudioAssetDifference>> CompareAsync(
        IAudioAssetCatalog active,
        IAudioAssetCatalog comparison,
        bool includeUnchanged,
        CancellationToken cancellationToken)
        => CompareAsync(active, comparison, includeUnchanged, compareHashes: true, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<AudioAssetDifference>> CompareAsync(
        IAudioAssetCatalog active,
        IAudioAssetCatalog comparison,
        bool includeUnchanged = false,
        bool compareHashes = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(comparison);
        var activeEntries = await active.BuildIndexAsync(false, cancellationToken).ConfigureAwait(false);
        var comparisonEntries = await comparison.BuildIndexAsync(false, cancellationToken).ConfigureAwait(false);
        var left = activeEntries
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CanonicalPath))
            .GroupBy(e => e.CanonicalPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var right = comparisonEntries
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CanonicalPath))
            .GroupBy(e => e.CanonicalPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(left.Keys, StringComparer.OrdinalIgnoreCase);
        paths.UnionWith(right.Keys);
        var result = new List<AudioAssetDifference>(paths.Count);
        foreach (string path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            left.TryGetValue(path, out AudioAssetEntry activeEntry);
            right.TryGetValue(path, out AudioAssetEntry comparisonEntry);
            var changes = FindMetadataDifferences(activeEntry, comparisonEntry);
            AudioAssetDifferenceKind kind = activeEntry is null ? AudioAssetDifferenceKind.Added
                : comparisonEntry is null ? AudioAssetDifferenceKind.Removed
                : changes.Count == 0 ? AudioAssetDifferenceKind.Unchanged : AudioAssetDifferenceKind.Changed;
            string activeEncodedHash = null;
            string comparisonEncodedHash = null;
            string activeDecodedHash = null;
            string comparisonDecodedHash = null;
            if (compareHashes && activeEntry != null && comparisonEntry != null)
            {
                (activeEncodedHash, activeDecodedHash) = await ComputeHashesAsync(active, activeEntry, cancellationToken)
                    .ConfigureAwait(false);
                (comparisonEncodedHash, comparisonDecodedHash) = await ComputeHashesAsync(comparison, comparisonEntry, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(activeEncodedHash) &&
                    !string.Equals(activeEncodedHash, comparisonEncodedHash, StringComparison.OrdinalIgnoreCase))
                    changes.Add("encoded content hash");
                if (!string.IsNullOrWhiteSpace(activeDecodedHash) &&
                    !string.Equals(activeDecodedHash, comparisonDecodedHash, StringComparison.OrdinalIgnoreCase))
                    changes.Add("decoded content hash");
                if (changes.Count > 0 && !changes.Contains("copy conflict", StringComparer.OrdinalIgnoreCase))
                    changes.Add("copy conflict");
                if (changes.Count > 0)
                    kind = AudioAssetDifferenceKind.Changed;
            }
            if (includeUnchanged || kind != AudioAssetDifferenceKind.Unchanged)
                result.Add(new AudioAssetDifference(kind, path, activeEntry, comparisonEntry, changes)
                {
                    CopyConflict = activeEntry != null && comparisonEntry != null && changes.Count > 0,
                    ActiveEncodedHash = activeEncodedHash,
                    ComparisonEncodedHash = comparisonEncodedHash,
                    ActiveDecodedHash = activeDecodedHash,
                    ComparisonDecodedHash = comparisonDecodedHash,
                });
        }
        return result;
    }

    private static async Task<(string Encoded, string Decoded)> ComputeHashesAsync(
        IAudioAssetCatalog catalog,
        AudioAssetEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            WzBinaryProperty property = await catalog.LoadPropertyAsync(entry, cancellationToken).ConfigureAwait(false);
            if (property == null)
                return (null, null);
            byte[] encoded = property.GetBytes(false);
            if (encoded == null || encoded.Length == 0)
                return (null, null);
            string encodedHash = Convert.ToHexString(SHA256.HashData(encoded)).ToLowerInvariant();
            string decodedHash = null;
            try
            {
                AudioDecodeResult decoded = await new NAudioCodecProvider().DecodeAsync(property, cancellationToken)
                    .ConfigureAwait(false);
                using var stream = new MemoryStream();
                foreach (float[] channel in decoded.Buffer.Samples)
                {
                    byte[] bytes = new byte[channel.Length * sizeof(float)];
                    Buffer.BlockCopy(channel, 0, bytes, 0, bytes.Length);
                    stream.Write(bytes, 0, bytes.Length);
                }
                decodedHash = Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
            }
            catch
            {
                // Encoded hash remains useful for unsupported/malformed audio;
                // comparison reports the metadata warning rather than failing
                // the entire source-set comparison.
            }
            return (encodedHash, decodedHash);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (null, null);
        }
    }

    private static List<string> FindMetadataDifferences(AudioAssetEntry left, AudioAssetEntry right)
    {
        if (left is null || right is null) return new List<string>();
        var changes = new List<string>();
        if (left.Metadata.DurationMilliseconds != right.Metadata.DurationMilliseconds) changes.Add("duration");
        if (left.Metadata.SampleRate != right.Metadata.SampleRate) changes.Add("sample rate");
        if (left.Metadata.ChannelCount != right.Metadata.ChannelCount) changes.Add("channels");
        if (!string.Equals(left.Metadata.Encoding, right.Metadata.Encoding, StringComparison.OrdinalIgnoreCase)) changes.Add("encoding");
        if (left.Metadata.PayloadSize != right.Metadata.PayloadSize) changes.Add("encoded content");
        return changes;
    }
}
