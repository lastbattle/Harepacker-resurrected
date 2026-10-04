#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

public sealed class AudioAiPromptCompiler
{
    public AudioAiBrief Compile(string userPrompt, string? role = null, string? mapContext = null,
        bool loop = false, double durationSeconds = 30)
    {
        var brief = new AudioAiBrief { Prompt = userPrompt.Trim(), Purpose = role ?? "MapleStory BGM", LoopIntent = loop, DurationSeconds = durationSeconds };
        brief.Instrumental = !ContainsVocalRequest(userPrompt);
        if (!string.IsNullOrWhiteSpace(mapContext)) brief.Prompt = $"{mapContext.Trim()}; {brief.Prompt}";
        if (!string.IsNullOrWhiteSpace(role)) brief.StemRole = role;
        return brief;
    }

    public string CompileProviderPrompt(AudioAiBrief brief, out List<string> fidelityWarnings)
    {
        brief.Validate(); fidelityWarnings = new();
        var prompt = brief.Prompt;
        if (brief.Instrumental) prompt += ", instrumental, no vocals";
        if (brief.LoopIntent) prompt += ", seamless loop with matching first and last bars";
        if (brief.Tempo is { } tempo) prompt += $", {tempo:0.#} BPM";
        if (!string.IsNullOrWhiteSpace(brief.KeyScale)) prompt += $", key {brief.KeyScale}";
        if (!string.IsNullOrWhiteSpace(brief.TimeSignature)) prompt += $", {brief.TimeSignature} meter";
        if (brief.Genres.Count > 0) prompt += ", " + string.Join(", ", brief.Genres);
        if (brief.Moods.Count > 0) prompt += ", " + string.Join(", ", brief.Moods);
        return prompt;
    }
    private static bool ContainsVocalRequest(string value) => new[] { "vocal", "sing", "lyrics", "voice" }.Any(value.Contains);
}

public interface IAudioAiSecretStore
{
    ValueTask SetAsync(string name, string value, CancellationToken cancellationToken = default);
    ValueTask<string?> GetAsync(string name, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default);
}

public sealed class EnvironmentAudioAiSecretStore : IAudioAiSecretStore
{
    private readonly string prefix;
    public EnvironmentAudioAiSecretStore(string prefix = "HAREPACKER_AUDIO_AI_") => this.prefix = prefix;
    public ValueTask SetAsync(string name, string value, CancellationToken cancellationToken = default) { Environment.SetEnvironmentVariable(prefix + name.ToUpperInvariant(), value); return ValueTask.CompletedTask; }
    public ValueTask<string?> GetAsync(string name, CancellationToken cancellationToken = default) => ValueTask.FromResult(Environment.GetEnvironmentVariable(prefix + name.ToUpperInvariant()));
    public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default) { Environment.SetEnvironmentVariable(prefix + name.ToUpperInvariant(), null); return ValueTask.CompletedTask; }
}

public sealed class AudioAiJobStore
{
    private readonly string root;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public AudioAiJobStore(string rootDirectory) => root = rootDirectory;
    public string GetJobDirectory(Guid id) => Path.Combine(root, id.ToString("N"));
    public async Task CreateAsync(AudioAiJobHandle handle, AudioAiRequest request, CancellationToken cancellationToken = default)
    {
        var directory = GetJobDirectory(handle.LocalJobId); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "request.json"), JsonSerializer.Serialize(request, json), cancellationToken).ConfigureAwait(false);
        await WriteStateAsync(handle.LocalJobId, new AudioAiPersistedState { State = AudioAiJobState.Queued }, cancellationToken).ConfigureAwait(false);
    }
    public async Task WriteStateAsync(Guid id, AudioAiPersistedState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetJobDirectory(id));
        await File.WriteAllTextAsync(Path.Combine(GetJobDirectory(id), "state.json"), JsonSerializer.Serialize(state, json), cancellationToken).ConfigureAwait(false);
    }
    public async Task<AudioAiPersistedState?> ReadStateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(GetJobDirectory(id), "state.json"); if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<AudioAiPersistedState>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), json);
    }
    public void Delete(Guid id) { var directory = GetJobDirectory(id); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

public sealed class AudioAiPersistedState
{
    public AudioAiJobState State { get; set; }
    public double Progress { get; set; }
    public string? Message { get; set; }
    public List<AudioAiArtifact> Candidates { get; set; } = new();
}

public static class AudioAiHashing
{
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path); using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();
    }
}
