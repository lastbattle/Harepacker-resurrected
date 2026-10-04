#nullable enable

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio.AI;

/// <summary>Text-only prompt planner. It deliberately cannot render or upload reference audio.</summary>
public sealed class OpenAICompatibleAudioPlanner : IDisposable
{
    private readonly HttpClient client; private readonly bool ownsClient; private readonly string endpoint;
    public OpenAICompatibleAudioPlanner(string endpoint = "https://openrouter.ai/api/v1", string? apiKey = null, HttpClient? httpClient = null)
    {
        this.endpoint = endpoint.TrimEnd('/'); client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; ownsClient = httpClient is null;
        if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
    public async Task<AudioAiBrief> PlanAsync(string request, string model = "openai/gpt-5.6-luna:xhigh", CancellationToken cancellationToken = default)
    {
        var payload = new { model, temperature = 0.2, messages = new[] { new { role = "system", content = "Return JSON only with prompt, negativePrompt, instrumental, durationSeconds, tempo, keyScale, timeSignature, loopIntent, genres, moods, instruments." }, new { role = "user", content = request } } };
        using var response = await client.PostAsJsonAsync(endpoint + "/chat/completions", payload, cancellationToken).ConfigureAwait(false); response.EnsureSuccessStatusCode();
        var root = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? throw new InvalidDataException("Planner returned no content.");
        var start = content.IndexOf('{'); var end = content.LastIndexOf('}'); if (start < 0 || end <= start) throw new InvalidDataException("Planner did not return a JSON brief.");
        var brief = JsonSerializer.Deserialize<AudioAiBrief>(content[start..(end + 1)], new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Planner returned an empty brief.");
        brief.Validate(); return brief;
    }
    public void Dispose() { if (ownsClient) client.Dispose(); }
}
