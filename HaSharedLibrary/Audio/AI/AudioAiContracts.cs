#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

[Flags]
public enum AudioAiCapability
{
    None = 0, TextToMusic = 1 << 0, TextToSound = 1 << 1, AudioToAudio = 1 << 2,
    Extend = 1 << 3, Repaint = 1 << 4, LoopAware = 1 << 5, SectionPlan = 1 << 6,
    AddLayer = 1 << 7, SeparateFixedStems = 1 << 8, SeparateByText = 1 << 9,
    Speech = 1 << 10, ScoreToSinging = 1 << 11, VoiceConversion = 1 << 12,
    AudioUnderstanding = 1 << 13, DeterministicSeed = 1 << 14, ProviderCancellation = 1 << 15,
}

public enum AudioAiProviderLocation { Local, Cloud, CustomEndpoint }
public enum AudioAiOperation { GenerateMusic, GenerateSound, Variation, Extend, Repaint, AddLayer, SeparateStems, SeparateByText, Speech, Singing, VoiceConversion, Analyze }
public enum AudioAiJobEventKind { Queued, Stage, Progress, Warning, Candidate, Completed, Failed, Cancelled }
public enum AudioAiJobState { Queued, Running, Completed, Failed, Cancelled }

public sealed record AudioAiLicenseDescriptor(string Identifier, string? SourceUrl = null, string? TextHash = null,
    DateTimeOffset? AcceptedAt = null);

public sealed class AudioAiModelInfo
{
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public AudioAiCapability Capabilities { get; set; }
    public int MaximumDurationSeconds { get; set; } = 600;
    public int MaximumCandidates { get; set; } = 4;
    public long MaximumReferenceBytes { get; set; } = 100 * 1024 * 1024;
    public List<string> InputFormats { get; set; } = new() { "wav", "mp3" };
    public List<string> OutputFormats { get; set; } = new() { "wav" };
    public List<string> MusicalControls { get; set; } = new();
    public AudioAiLicenseDescriptor? License { get; set; }
}

public sealed class AudioAiProviderInfo
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AudioAiProviderLocation Location { get; set; }
    public bool Healthy { get; set; }
    public string? Version { get; set; }
    public List<AudioAiModelInfo> Models { get; set; } = new();
    public string? TermsUrl { get; set; }
    public string? RetentionDescription { get; set; }
    public bool? TrainsOnInput { get; set; }
}

public sealed class AudioAiBrief
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public AudioAiOperation Operation { get; set; } = AudioAiOperation.GenerateMusic;
    public string Purpose { get; set; } = "MapleStory BGM";
    public bool Instrumental { get; set; } = true;
    public string Prompt { get; set; } = string.Empty;
    public string? NegativePrompt { get; set; }
    public double DurationSeconds { get; set; } = 30;
    public double? Tempo { get; set; }
    public string? KeyScale { get; set; }
    public string TimeSignature { get; set; } = "4/4";
    public bool LoopIntent { get; set; }
    public string? StemRole { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Moods { get; set; } = new();
    public List<string> Instruments { get; set; } = new();
    public List<string> ExcludedInstruments { get; set; } = new();
    public List<AudioAiInputArtifact> ReferenceArtifacts { get; set; } = new();
    public AudioAiRange? EditRange { get; set; }
    public Dictionary<string, JsonElement> Extensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException($"Unsupported Audio AI brief schema {SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(Prompt)) throw new InvalidDataException("An Audio AI prompt is required.");
        if (DurationSeconds is <= 0 or > 600) throw new InvalidDataException("Audio AI duration must be between 0 and 600 seconds.");
    }
}

public sealed record AudioAiRange(long StartSample, long LengthSamples);
public sealed class AudioAiInputArtifact
{
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString("N");
    public string ContentHash { get; set; } = string.Empty;
    public string? LocalPath { get; set; }
    public AudioAiRange? Range { get; set; }
    public long ByteLength { get; set; }
}

public sealed class UploadAuthorization
{
    public string ProviderId { get; set; } = string.Empty;
    public List<string> ArtifactIds { get; set; } = new();
    public long AuthorizedBytes { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Matches(string provider, IEnumerable<AudioAiInputArtifact> inputs) =>
        string.Equals(provider, ProviderId, StringComparison.OrdinalIgnoreCase) && ExpiresAt > DateTimeOffset.UtcNow &&
        inputs.All(input => ArtifactIds.Contains(input.ArtifactId, StringComparer.OrdinalIgnoreCase)) &&
        inputs.Sum(input => input.ByteLength) <= AuthorizedBytes;
}

public sealed class AudioAiRequest
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public int CanonicalVersion { get; set; } = 1;
    public string? ModelId { get; set; }
    public AudioAiBrief Brief { get; set; } = new();
    public int CandidateCount { get; set; } = 1;
    public long? Seed { get; set; }
    public string OutputFormat { get; set; } = "wav";
    public UploadAuthorization? UploadAuthorization { get; set; }
    public Dictionary<string, JsonElement> ProviderOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record AudioAiJobHandle(Guid LocalJobId, string? ProviderJobId = null, bool Resumable = false);
public sealed record AudioAiJobEvent(AudioAiJobEventKind Kind, DateTimeOffset Timestamp, double? Progress = null,
    string? Message = null, AudioAiArtifact? Artifact = null);

public sealed class AudioAiArtifact
{
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString("N");
    public string ContentHash { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string Format { get; set; } = "wav";
    public string? Role { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ModelRevision { get; set; }
    public long? Seed { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public AudioAiProvenance Provenance { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
}

public sealed class AudioAiProvenance
{
    public int CanonicalVersion { get; set; } = 1;
    public string Operation { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string? LicenseIdentifier { get; set; }
    public string? TermsUrl { get; set; }
    public DateTimeOffset? TermsCheckedAt { get; set; }
    public bool InputLeftMachine { get; set; }
    public List<string> InputHashes { get; set; } = new();
    public Dictionary<string, JsonElement> ProviderMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public interface IAudioAiProvider
{
    string ProviderId { get; }
    Task<AudioAiProviderInfo> GetInfoAsync(CancellationToken cancellationToken);
    Task<AudioAiJobHandle> StartAsync(AudioAiRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<AudioAiJobEvent> WatchAsync(AudioAiJobHandle job, CancellationToken cancellationToken);
    Task CancelAsync(AudioAiJobHandle job, CancellationToken cancellationToken);
}

public sealed class AudioAiProviderRegistry
{
    private readonly Dictionary<string, IAudioAiProvider> providers = new(StringComparer.OrdinalIgnoreCase);
    public void Register(IAudioAiProvider provider) => providers[provider.ProviderId] = provider;
    public bool TryGet(string id, out IAudioAiProvider? provider) => providers.TryGetValue(id, out provider);

    public async Task<IAudioAiProvider> SelectAsync(AudioAiCapability required, bool localOnly,
        string? preferredProvider, CancellationToken cancellationToken)
    {
        var eligible = new List<(IAudioAiProvider Provider, AudioAiProviderInfo Info)>();
        foreach (var provider in providers.Values)
        {
            var info = await provider.GetInfoAsync(cancellationToken).ConfigureAwait(false);
            if (!info.Healthy || (localOnly && info.Location == AudioAiProviderLocation.Cloud)) continue;
            if (!info.Models.Any(model => (model.Capabilities & required) == required)) continue;
            eligible.Add((provider, info));
        }
        var selected = eligible.OrderByDescending(item => string.Equals(item.Provider.ProviderId, preferredProvider, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Info.Location == AudioAiProviderLocation.Cloud).FirstOrDefault();
        return selected.Provider ?? throw new InvalidOperationException($"No healthy Audio AI provider supports {required} under the selected policy.");
    }
}

public sealed class FakeAudioAiProvider : IAudioAiProvider
{
    private readonly string artifactPath;
    private readonly Dictionary<Guid, bool> cancelled = new();
    public FakeAudioAiProvider(string artifactPath) => this.artifactPath = artifactPath;
    public string ProviderId => "fake-local";
    public Task<AudioAiProviderInfo> GetInfoAsync(CancellationToken cancellationToken) => Task.FromResult(new AudioAiProviderInfo
    {
        ProviderId = ProviderId, DisplayName = "Fake local provider", Location = AudioAiProviderLocation.Local, Healthy = true,
        Models = { new AudioAiModelInfo { ModelId = "fixture", DisplayName = "Fixture", Capabilities = AudioAiCapability.TextToMusic | AudioAiCapability.ProviderCancellation } }
    });
    public Task<AudioAiJobHandle> StartAsync(AudioAiRequest request, CancellationToken cancellationToken)
    { request.Brief.Validate(); var id = Guid.NewGuid(); cancelled[id] = false; return Task.FromResult(new AudioAiJobHandle(id, id.ToString("N"), true)); }
    public async IAsyncEnumerable<AudioAiJobEvent> WatchAsync(AudioAiJobHandle job, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new(AudioAiJobEventKind.Queued, DateTimeOffset.UtcNow, 0);
        await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
        if (cancelled.GetValueOrDefault(job.LocalJobId)) { yield return new(AudioAiJobEventKind.Cancelled, DateTimeOffset.UtcNow); yield break; }
        yield return new(AudioAiJobEventKind.Progress, DateTimeOffset.UtcNow, .5, "Generating fixture");
        var artifact = new AudioAiArtifact { LocalPath = artifactPath, ProviderId = ProviderId, ModelId = "fixture" };
        yield return new(AudioAiJobEventKind.Candidate, DateTimeOffset.UtcNow, 1, Artifact: artifact);
        yield return new(AudioAiJobEventKind.Completed, DateTimeOffset.UtcNow, 1);
    }
    public Task CancelAsync(AudioAiJobHandle job, CancellationToken cancellationToken) { cancelled[job.LocalJobId] = true; return Task.CompletedTask; }
}
