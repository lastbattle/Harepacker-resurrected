#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

public enum AudioAiNetworkPolicy { LocalOnly, AskBeforeUpload, CloudAllowed }

public sealed class AudioAiSettings
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public AudioAiNetworkPolicy NetworkPolicy { get; set; } = AudioAiNetworkPolicy.LocalOnly;
    public string PreferredLocalProvider { get; set; } = "ace-step-local";
    public string? PreferredModelId { get; set; }
    public string LocalEndpoint { get; set; } = "http://127.0.0.1:8765";
    public bool InstrumentalByDefault { get; set; } = true;
    public Dictionary<string, JsonElement> ProviderOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public void Validate() { if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException($"Unsupported Audio AI settings schema {SchemaVersion}."); }
}

public sealed class AudioAiCandidateService
{
    public async Task<AudioAiArtifact> ValidateAsync(AudioAiArtifact artifact, long maximumBytes = 512 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        if (artifact is null || string.IsNullOrWhiteSpace(artifact.LocalPath)) throw new InvalidDataException("The AI artifact has no local path.");
        var file = new System.IO.FileInfo(artifact.LocalPath);
        if (!file.Exists) throw new FileNotFoundException("The AI artifact is missing.", artifact.LocalPath);
        if (file.Length > maximumBytes) throw new InvalidDataException("The AI artifact exceeds the configured size limit.");
        artifact.ContentHash = await AudioAiHashing.Sha256Async(file.FullName, cancellationToken).ConfigureAwait(false);
        return artifact;
    }
}
