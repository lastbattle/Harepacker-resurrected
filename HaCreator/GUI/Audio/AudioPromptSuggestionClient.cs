#nullable enable

using HaCreator.MapEditor.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.GUI.Audio;

/// <summary>Rewrites a raw music idea into a concise, production-oriented generation brief.</summary>
public sealed class AudioPromptSuggestionClient : IDisposable
{
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly string baseUrl;
    private readonly string apiKey;
    private readonly string model;
    private readonly AIEndpointProtocol protocol;

    public AudioPromptSuggestionClient(HttpClient? httpClient = null)
    {
        baseUrl = AISettings.BaseUrl;
        apiKey = AISettings.ApiKey;
        model = AISettings.Model;
        protocol = AISettings.Protocol;
        client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        ownsClient = httpClient is null;
    }

    public async Task<string> SuggestAsync(string rawBrief, double durationSeconds, bool loop,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawBrief))
            throw new ArgumentException("A music brief is required.", nameof(rawBrief));
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("A text model is required for prompt refinement.");

        const string systemPrompt =
            "Rewrite the user's raw idea as one concise, production-ready prompt for a text-to-music model. " +
            "Preserve the user's setting, mood, genre, era, and musical intent. Add useful specificity for " +
            "instrumentation, rhythm, approximate tempo, arrangement arc, timbre, mix, and game-scene function " +
            "only when compatible with the request. Prefer instrumental background music unless vocals are " +
            "explicitly requested. Describe a clean loop transition when loop mode is requested. Do not name or " +
            "imitate living artists, copyrighted songs, or franchises. Do not add explanations, headings, lists, " +
            "quotes, negative prompts, or metadata. Return only the final music-generation prompt.";
        string context = $"Requested duration: {durationSeconds:0.###} seconds\n" +
            $"Loop mode: {(loop ? "enabled" : "disabled")}\nRaw music idea: {rawBrief.Trim()}";

        JObject body;
        string path;
        if (protocol == AIEndpointProtocol.Responses)
        {
            path = "responses";
            body = new JObject
            {
                ["model"] = model,
                ["instructions"] = systemPrompt,
                ["input"] = context,
                ["max_output_tokens"] = 500,
            };
        }
        else
        {
            path = "chat/completions";
            body = new JObject
            {
                ["model"] = model,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = context },
                },
                ["max_tokens"] = 500,
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(path));
        request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        using HttpResponseMessage response = await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ReadProviderError(responseBody) ??
                $"Prompt API error: {(int)response.StatusCode} {response.ReasonPhrase}.");

        JObject payload;
        try { payload = JObject.Parse(responseBody); }
        catch (JsonException exception) { throw new InvalidOperationException("The prompt API returned invalid JSON.", exception); }
        string? result = protocol == AIEndpointProtocol.Responses
            ? ReadResponsesText(payload)
            : payload["choices"]?[0]?["message"]?["content"]?.ToString();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("The prompt API returned no refined brief.");
        return result.Trim();
    }

    private Uri BuildEndpoint(string path)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? root) ||
            (root.Scheme != Uri.UriSchemeHttp && root.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("A valid HTTP(S) AI base URL is required.");
        return new Uri(new Uri(root.ToString().TrimEnd('/') + "/"), path);
    }

    private static string? ReadResponsesText(JObject payload)
    {
        string? direct = payload["output_text"]?.ToString();
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        return string.Join("\n", (payload["output"] as JArray ?? new JArray())
            .SelectMany(item => item["content"] as JArray ?? new JArray())
            .Select(item => item["text"]?.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? ReadProviderError(string responseBody)
    {
        try { return JObject.Parse(responseBody)["error"]?["message"]?.ToString(); }
        catch (JsonException) { return null; }
    }

    public void Dispose() { if (ownsClient) client.Dispose(); }
}
