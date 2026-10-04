#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

/// <summary>Thin, vendor-neutral REST adapter for an ACE-Step-compatible loopback sidecar.</summary>
public sealed class AceStepLocalAudioAiProvider : IAudioAiProvider, IDisposable
{
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly string endpoint;
    private readonly string? token;

    public AceStepLocalAudioAiProvider(string endpoint = "http://127.0.0.1:8765", string? bearerToken = null,
        HttpClient? httpClient = null)
    {
        this.endpoint = endpoint.TrimEnd('/');
        token = bearerToken;
        client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        ownsClient = httpClient is null;
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public string ProviderId => "ace-step-local";

    public async Task<AudioAiProviderInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(endpoint + "/health", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return new AudioAiProviderInfo { ProviderId = ProviderId, DisplayName = "ACE-Step (local)", Location = AudioAiProviderLocation.CustomEndpoint, Healthy = false };
        var health = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var info = new AudioAiProviderInfo { ProviderId = ProviderId, DisplayName = "ACE-Step 1.5 (local)", Location = AudioAiProviderLocation.CustomEndpoint, Healthy = true,
            Version = TryString(Unwrap(health), "version") };
        try
        {
            using var modelResponse = await client.GetAsync(endpoint + "/v1/models", cancellationToken).ConfigureAwait(false);
            if (modelResponse.IsSuccessStatusCode)
            {
                var models = await modelResponse.Content.ReadFromJsonAsync<List<AudioAiModelInfo>>(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (models is not null) info.Models = models;
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
        if (info.Models.Count == 0)
            info.Models.Add(new AudioAiModelInfo { ModelId = "ace-step1.5", DisplayName = "ACE-Step 1.5", Capabilities =
                AudioAiCapability.TextToMusic | AudioAiCapability.AudioToAudio | AudioAiCapability.Extend | AudioAiCapability.Repaint |
                AudioAiCapability.LoopAware | AudioAiCapability.AddLayer | AudioAiCapability.DeterministicSeed,
                License = new AudioAiLicenseDescriptor("MIT") });
        return info;
    }

    public async Task<AudioAiJobHandle> StartAsync(AudioAiRequest request, CancellationToken cancellationToken)
    {
        request.Brief.Validate();
        if (request.Brief.ReferenceArtifacts.Count > 0 && request.UploadAuthorization is null)
            throw new InvalidOperationException("A local reference authorization is required before staging reference audio.");
        var providerPrompt = new AudioAiPromptCompiler().CompileProviderPrompt(request.Brief, out _);
        var providerRequest = new
        {
            prompt = providerPrompt,
            lyrics = request.Brief.Instrumental ? "[Instrumental]" : string.Empty,
            thinking = false,
            bpm = request.Brief.Tempo is null ? (int?)null : (int)Math.Round(request.Brief.Tempo.Value),
            key_scale = request.Brief.KeyScale ?? string.Empty,
            time_signature = request.Brief.TimeSignature,
            audio_duration = request.Brief.DurationSeconds,
            batch_size = request.CandidateCount,
            use_random_seed = request.Seed is null,
            seed = request.Seed ?? -1,
            audio_format = request.OutputFormat,
            model = request.ModelId,
            task_type = TranslateTask(request.Brief.Operation),
            reference_audio_path = request.Brief.ReferenceArtifacts.Count > 0 ? request.Brief.ReferenceArtifacts[0].LocalPath : null,
        };
        using var response = await client.PostAsJsonAsync(endpoint + "/release_task", providerRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
        payload = Unwrap(payload);
        var providerId = TryString(payload, "task_id") ?? TryString(payload, "job_id") ?? TryString(payload, "id") ?? throw new InvalidDataException("ACE-Step response did not contain a task id.");
        return new AudioAiJobHandle(request.OperationId, providerId, true);
    }

    public async IAsyncEnumerable<AudioAiJobEvent> WatchAsync(AudioAiJobHandle job,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            using var response = await client.PostAsJsonAsync(endpoint + "/query_result", new { task_id_list = new[] { job.ProviderJobId ?? job.LocalJobId.ToString("N") } }, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
            payload = Unwrap(payload);
            if (payload.ValueKind == JsonValueKind.Array && payload.GetArrayLength() > 0) payload = payload[0];
            var status = payload.TryGetProperty("status", out var statusValue) && statusValue.TryGetInt32(out var statusCode) ? statusCode : 0;
            var progress = TryDouble(payload, "progress");
            if (status == 1)
            {
                List<AudioAiArtifact> artifacts = ParseResultArtifacts(payload).ToList();
                if (artifacts.Count == 0)
                {
                    yield return new(AudioAiJobEventKind.Failed, DateTimeOffset.UtcNow, 1,
                        TryString(payload, "progress_text") ?? "ACE-Step completed without producing an audio file.");
                    yield break;
                }
                foreach (var artifact in artifacts)
                    yield return new(AudioAiJobEventKind.Candidate, DateTimeOffset.UtcNow, 1, Artifact: artifact);
                yield return new(AudioAiJobEventKind.Completed, DateTimeOffset.UtcNow, 1);
                yield break;
            }
            if (status == 2) { yield return new(AudioAiJobEventKind.Failed, DateTimeOffset.UtcNow, progress, TryString(payload, "progress_text")); yield break; }
            yield return new(AudioAiJobEventKind.Progress, DateTimeOffset.UtcNow, progress, TryString(payload, "progress_text") ?? "running");
            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CancelAsync(AudioAiJobHandle job, CancellationToken cancellationToken)
    {
        // Upstream 1.5 currently exposes local polling but no stable per-job cancel route.
        // Cancelling the caller stops polling; the UI must disclose that compute may continue.
        await Task.CompletedTask;
    }

    private static IEnumerable<AudioAiArtifact> ParseResultArtifacts(JsonElement payload)
    {
        if (!payload.TryGetProperty("result", out var resultValue)) yield break;
        JsonElement values;
        try { values = resultValue.ValueKind == JsonValueKind.String ? JsonSerializer.Deserialize<JsonElement>(resultValue.GetString() ?? "[]") : resultValue; }
        catch (JsonException) { yield break; }
        if (values.ValueKind != JsonValueKind.Array) yield break;
        foreach (var value in values.EnumerateArray())
        {
            var path = NormalizeArtifactPath(TryString(value, "file") ?? TryString(value, "path"));
            if (string.IsNullOrWhiteSpace(path)) continue;
            yield return new AudioAiArtifact { LocalPath = path, ProviderId = "ace-step-local", ModelId = "ace-step1.5", Provenance = new AudioAiProvenance { ProviderId = "ace-step-local", ModelId = "ace-step1.5" } };
        }
    }
    private static JsonElement Unwrap(JsonElement value) => value.TryGetProperty("data", out var data) ? data : value;
    private static string? NormalizeArtifactPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        const string marker = "/v1/audio?path=";
        if (value.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            var encoded = value[marker.Length..];
            var ampersand = encoded.IndexOf('&');
            if (ampersand >= 0) encoded = encoded[..ampersand];
            return Uri.UnescapeDataString(encoded);
        }
        return value;
    }
    private static string TranslateTask(AudioAiOperation operation) => operation switch { AudioAiOperation.Extend => "complete", AudioAiOperation.Repaint => "repaint", AudioAiOperation.Variation => "cover", AudioAiOperation.AddLayer => "lego", AudioAiOperation.SeparateStems => "extract", _ => "text2music" };
    private static string? TryString(JsonElement value, string property) => value.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static double? TryDouble(JsonElement value, string property) => value.TryGetProperty(property, out var p) && p.TryGetDouble(out var d) ? d : null;
    public void Dispose() { if (ownsClient) client.Dispose(); }
}
