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

namespace HaCreator.GUI.FrameAnimation.AI
{
    public sealed class AnimationPromptSuggestionOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public AIEndpointProtocol Protocol { get; set; } = AIEndpointProtocol.ChatCompletions;

        public static AnimationPromptSuggestionOptions FromSettings() => new()
        {
            BaseUrl = AISettings.BaseUrl,
            ApiKey = AISettings.ApiKey,
            Model = AISettings.Model,
            Protocol = AISettings.Protocol
        };
    }

    /// <summary>
    /// Uses the configured text model to turn a short request into a production-ready
    /// sprite prompt. This intentionally has no map tools or command execution surface.
    /// </summary>
    public sealed class AnimationPromptSuggestionClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly bool ownsHttpClient;
        private readonly AnimationPromptSuggestionOptions options;

        public AnimationPromptSuggestionClient(AnimationPromptSuggestionOptions options = null, HttpClient httpClient = null)
        {
            this.options = options ?? AnimationPromptSuggestionOptions.FromSettings();
            this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            ownsHttpClient = httpClient == null;
        }

        public async Task<string> SuggestAsync(string request, string animationContext,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request))
                throw new ArgumentException("A prompt request is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(options.Model))
                throw new InvalidOperationException("A text model is required for prompt suggestions.");

            string systemPrompt =
                "You write concise production prompts for 2D MapleStory-style animation frames. " +
                "Preserve the described subject and action, require a single isolated sprite, crisp readable silhouette, " +
                "consistent scale and camera, generous padding, no text, no watermark, no cast shadow, and a perfectly " +
                "flat solid magenta background (#FF00FF) suitable for deterministic removal. Return only the final prompt.";
            string userPrompt = $"Animation context: {animationContext}\nUser request: {request.Trim()}";

            JObject body;
            string relativePath;
            if (options.Protocol == AIEndpointProtocol.Responses)
            {
                relativePath = "responses";
                body = new JObject
                {
                    ["model"] = options.Model,
                    ["instructions"] = systemPrompt,
                    ["input"] = userPrompt,
                    ["max_output_tokens"] = 700
                };
            }
            else
            {
                relativePath = "chat/completions";
                body = new JObject
                {
                    ["model"] = options.Model,
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "system", ["content"] = systemPrompt },
                        new JObject { ["role"] = "user", ["content"] = userPrompt }
                    },
                    ["max_tokens"] = 700
                };
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(relativePath));
            message.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());

            using HttpResponseMessage response = await httpClient.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(ReadProviderError(responseBody) ??
                    $"Prompt API error: {(int)response.StatusCode} {response.ReasonPhrase}.");

            JObject payload;
            try { payload = JObject.Parse(responseBody); }
            catch (JsonException ex) { throw new InvalidOperationException("The prompt API returned invalid JSON.", ex); }

            string result = options.Protocol == AIEndpointProtocol.Responses
                ? ReadResponsesText(payload)
                : payload["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException("The prompt API returned no suggestion.");
            return result.Trim();
        }

        private Uri BuildEndpoint(string relativePath)
        {
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("A valid HTTP(S) AI base URL is required.");
            return new Uri(new Uri(baseUri.ToString().TrimEnd('/') + "/"), relativePath);
        }

        private static string ReadResponsesText(JObject payload)
        {
            string direct = payload["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;
            return string.Join("\n", (payload["output"] as JArray ?? new JArray())
                .SelectMany(item => item["content"] as JArray ?? new JArray())
                .Select(item => item["text"]?.ToString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        private static string ReadProviderError(string responseBody)
        {
            try { return JObject.Parse(responseBody)["error"]?["message"]?.ToString(); }
            catch (JsonException) { return null; }
        }

        public void Dispose()
        {
            if (ownsHttpClient)
                httpClient.Dispose();
        }
    }
}
