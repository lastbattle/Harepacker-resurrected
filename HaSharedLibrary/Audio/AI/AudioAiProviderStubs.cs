#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

/// <summary>Named provider shells keep hosted integrations capability-visible until credentials and terms are configured.</summary>
public class UnavailableAudioAiProvider : IAudioAiProvider
{
    private readonly string id; private readonly string name; private readonly AudioAiCapability capabilities;
    public UnavailableAudioAiProvider(string providerId, string displayName, AudioAiCapability capabilities = AudioAiCapability.None) { id = providerId; name = displayName; this.capabilities = capabilities; }
    public string ProviderId => id;
    public virtual Task<AudioAiProviderInfo> GetInfoAsync(CancellationToken cancellationToken) => Task.FromResult(new AudioAiProviderInfo { ProviderId = id, DisplayName = name, Location = AudioAiProviderLocation.Cloud, Healthy = false, Models = { new AudioAiModelInfo { ModelId = id, DisplayName = name, Capabilities = capabilities } } });
    public Task<AudioAiJobHandle> StartAsync(AudioAiRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException($"Provider '{id}' is not configured.");
    public async IAsyncEnumerable<AudioAiJobEvent> WatchAsync(AudioAiJobHandle job, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) { await Task.CompletedTask; yield break; }
    public Task CancelAsync(AudioAiJobHandle job, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class StabilityAudioAiProvider : UnavailableAudioAiProvider
{
    public StabilityAudioAiProvider() : base("stability-audio", "Stable Audio", AudioAiCapability.TextToMusic | AudioAiCapability.AudioToAudio | AudioAiCapability.Extend) { }
}

public sealed class ElevenMusicAudioAiProvider : UnavailableAudioAiProvider
{
    public ElevenMusicAudioAiProvider() : base("eleven-music", "Eleven Music", AudioAiCapability.TextToMusic | AudioAiCapability.SectionPlan | AudioAiCapability.Extend | AudioAiCapability.Repaint) { }
}

public sealed class LocalStemSeparationProvider : UnavailableAudioAiProvider
{
    public LocalStemSeparationProvider() : base("local-stem-separation", "Local stem separation", AudioAiCapability.SeparateFixedStems) { }
}

public sealed class SamAudioSeparationProvider : UnavailableAudioAiProvider
{
    public SamAudioSeparationProvider() : base("sam-audio-experimental", "SAM-Audio (experimental)", AudioAiCapability.SeparateByText) { }
}
