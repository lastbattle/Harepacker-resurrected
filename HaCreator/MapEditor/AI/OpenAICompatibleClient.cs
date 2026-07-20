using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    public enum AIEndpointProtocol
    {
        ChatCompletions,
        Responses
    }

    public sealed class OpenAICompatibleOptions
    {
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public AIEndpointProtocol Protocol { get; set; } = AIEndpointProtocol.ChatCompletions;
        public string ReasoningEffort { get; set; } = string.Empty;
        public bool StrictSchemas { get; set; }
        public int MaxToolTurns { get; set; } = 40;
        public int MaxOutputTokens { get; set; } = 100000;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Model metadata returned by an OpenAI-compatible /models endpoint.
    /// Most standard endpoints only return Id; compatible gateways may also expose
    /// supported reasoning-effort values.
    /// </summary>
    public sealed class OpenAIModelInfo
    {
        public OpenAIModelInfo(string id, IReadOnlyList<string> reasoningEfforts)
        {
            Id = id;
            ReasoningEfforts = reasoningEfforts ?? Array.Empty<string>();
        }

        public string Id { get; }
        public IReadOnlyList<string> ReasoningEfforts { get; }
    }

    /// <summary>
    /// Provider-neutral client for OpenAI-compatible APIs.
    /// OpenRouter is represented by its default base URL; custom RPC endpoints use the same code path.
    /// </summary>
    public sealed class OpenAICompatibleClient : IAIClient, IDisposable
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly OpenAICompatibleOptions options;
        private readonly MapMcpToolServer toolServer;
        private readonly bool ownsToolServer;

        public OpenAICompatibleClient(OpenAICompatibleOptions options, MapMcpToolServer toolServer = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.toolServer = toolServer ?? new MapMcpToolServer();
            ownsToolServer = toolServer == null;
        }

        public async Task<string> ProcessInstructionsAsync(
            string mapContext,
            string userInstructions,
            CancellationToken cancellationToken = default)
        {
            ValidateConfiguration();
            toolServer.ResetConversationState();

            var systemPrompt = MapEditorPromptBuilder.LoadSystemPrompt();
            var userMessage = MapEditorPromptBuilder.BuildUserMessage(mapContext, userInstructions);

            return options.Protocol == AIEndpointProtocol.Responses
                ? await RunResponsesAsync(systemPrompt, userMessage, cancellationToken).ConfigureAwait(false)
                : await RunChatCompletionsAsync(systemPrompt, userMessage, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                ValidateConfiguration();
                JObject body;
                if (options.Protocol == AIEndpointProtocol.Responses)
                {
                    body = new JObject
                    {
                        ["model"] = options.Model,
                        ["input"] = "Say OK",
                        ["max_output_tokens"] = 10
                    };
                }
                else
                {
                    body = new JObject
                    {
                        ["model"] = options.Model,
                        ["messages"] = new JArray
                        {
                            new JObject { ["role"] = "user", ["content"] = "Say OK" }
                        },
                        ["max_tokens"] = 10
                    };
                }

                using (var response = await SendAsync(body, CancellationToken.None).ConfigureAwait(false))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAICompatibleClient] Connection test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fetch model identifiers from the configured OpenAI-compatible endpoint.
        /// This uses GET /models independently of the selected chat dialect.
        /// </summary>
        public async Task<IReadOnlyList<string>> GetModelsAsync(
            CancellationToken cancellationToken = default)
        {
            var catalog = await GetModelCatalogAsync(cancellationToken).ConfigureAwait(false);
            return catalog.Select(model => model.Id).ToList();
        }

        /// <summary>
        /// Fetch model identifiers and any optional capability metadata exposed by /models.
        /// </summary>
        public async Task<IReadOnlyList<OpenAIModelInfo>> GetModelCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            ValidateEndpoint();

            using (var response = await SendGetAsync(BuildModelsEndpointUrl(), cancellationToken).ConfigureAwait(false))
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new OpenAICompatibleApiException(
                        $"AI model list error: {(int)response.StatusCode} {response.ReasonPhrase}",
                        response.StatusCode,
                        content);
                }

                try
                {
                    var payload = JObject.Parse(content);
                    var models = new Dictionary<string, OpenAIModelInfo>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in payload["data"] as JArray ?? new JArray())
                    {
                        var modelId = item.Type == JTokenType.String
                            ? item.ToString()
                            : item["id"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(modelId))
                        {
                            modelId = modelId.Trim();
                            models[modelId] = new OpenAIModelInfo(
                                modelId,
                                ExtractReasoningEfforts(item as JObject));
                        }
                    }

                    return models.Values
                        .OrderBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch (JsonException ex)
                {
                    throw new OpenAICompatibleApiException(
                        $"AI model catalog returned invalid JSON: {ex.Message}",
                        response.StatusCode,
                        content,
                        ex);
                }
            }
        }

        private static IReadOnlyList<string> ExtractReasoningEfforts(JObject model)
        {
            if (model == null)
                return Array.Empty<string>();

            var efforts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in model.Properties())
            {
                if (property.Name.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                AddReasoningEfforts(property.Value, efforts);
            }

            if (efforts.Count == 0 && ContainsReasoningParameter(model["supported_parameters"]))
            {
                foreach (var effort in AISettings.AvailableReasoningEfforts)
                    efforts.Add(effort);
            }

            return efforts
                .OrderBy(effort => effort, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ContainsReasoningParameter(JToken token)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.String)
                return token.ToString().IndexOf("reasoning_effort", StringComparison.OrdinalIgnoreCase) >= 0;

            if (token is JArray array)
                return array.Any(ContainsReasoningParameter);

            if (token is JObject obj)
            {
                return obj.Properties().Any(property =>
                    property.Name.IndexOf("reasoning_effort", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ContainsReasoningParameter(property.Value));
            }

            return false;
        }

        private static void AddReasoningEfforts(JToken token, ISet<string> efforts)
        {
            if (token == null)
                return;

            if (token.Type == JTokenType.String)
            {
                foreach (var value in token.ToString().Split(',', ';', '|', ' ', '\t', '\r', '\n'))
                {
                    var normalized = value.Trim().ToLowerInvariant();
                    if (AISettings.AvailableReasoningEfforts.Contains(normalized))
                        efforts.Add(normalized);
                }
                return;
            }

            if (token is JArray array)
            {
                foreach (var item in array)
                    AddReasoningEfforts(item, efforts);
                return;
            }

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Name.Equals("values", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("options", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("allowed", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("supported", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        AddReasoningEfforts(property.Value, efforts);
                    }
                }
            }
        }

        /// <summary>
        /// Complete a text-only request for orchestration/planning prompts.
        /// </summary>
        public async Task<string> CompleteTextAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            ValidateConfiguration();
            JObject body;
            if (options.Protocol == AIEndpointProtocol.Responses)
            {
                body = new JObject
                {
                    ["model"] = options.Model,
                    ["instructions"] = systemPrompt,
                    ["input"] = userMessage,
                    ["max_output_tokens"] = Math.Min(options.MaxOutputTokens, 4000)
                };
            }
            else
            {
                body = new JObject
                {
                    ["model"] = options.Model,
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "system", ["content"] = systemPrompt },
                        new JObject { ["role"] = "user", ["content"] = userMessage }
                    },
                    ["max_tokens"] = Math.Min(options.MaxOutputTokens, 4000)
                };
            }

            AddReasoningEffort(body);
            var response = await SendAndParseAsync(body, cancellationToken).ConfigureAwait(false);
            return options.Protocol == AIEndpointProtocol.Responses
                ? GetResponsesText(response)
                : response["choices"]?[0]?["message"]?["content"]?.ToString();
        }

        public void Dispose()
        {
            if (ownsToolServer)
                toolServer.Dispose();
        }

        private async Task<string> RunChatCompletionsAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken cancellationToken)
        {
            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = userMessage }
            };

            var commands = new List<string>();
            for (var turn = 0; turn < options.MaxToolTurns; turn++)
            {
                var body = new JObject
                {
                    ["model"] = options.Model,
                    ["messages"] = messages,
                    ["tools"] = toolServer.GetChatCompletionTools(options.StrictSchemas),
                    ["tool_choice"] = turn == 0 ? "required" : "auto",
                    ["max_tokens"] = options.MaxOutputTokens
                };

                AddReasoningEffort(body);
                var response = await SendAndParseAsync(body, cancellationToken).ConfigureAwait(false);
                var message = response["choices"]?[0]?["message"] as JObject;
                if (message == null)
                    return commands.Count == 0 ? "# No response from AI" : string.Join(Environment.NewLine, commands);

                var toolCalls = message["tool_calls"] as JArray;
                if (toolCalls == null || toolCalls.Count == 0)
                {
                    if (commands.Count > 0)
                        return string.Join(Environment.NewLine, commands);

                    var text = message["content"]?.ToString();
                    return string.IsNullOrWhiteSpace(text)
                        ? "# No commands generated"
                        : $"# AI Response (no function calls):{Environment.NewLine}{text}";
                }

                messages.Add(message.DeepClone());
                foreach (var call in toolCalls.OfType<JObject>())
                {
                    var function = call["function"] as JObject;
                    var name = function?["name"]?.ToString();
                    var callId = call["id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var arguments = ParseArguments(function?["arguments"]?.ToString());
                    var result = toolServer.CallTool(name, arguments);
                    if (!result.Success && !result.IsQuery)
                        commands.Add($"# {name}: {result.Text}");
                    else if (!string.IsNullOrWhiteSpace(result.Command))
                        commands.Add(result.Command);

                    messages.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = callId,
                        ["content"] = result.Text ?? string.Empty
                    });
                }
            }

            return commands.Count == 0
                ? "# Tool loop stopped after reaching the safety limit"
                : string.Join(Environment.NewLine, commands);
        }

        private async Task<string> RunResponsesAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken cancellationToken)
        {
            var input = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = userMessage
                }
            };
            var commands = new List<string>();

            for (var turn = 0; turn < options.MaxToolTurns; turn++)
            {
                var body = new JObject
                {
                    ["model"] = options.Model,
                    ["instructions"] = systemPrompt,
                    ["input"] = input,
                    ["tools"] = toolServer.GetResponsesTools(options.StrictSchemas),
                    ["max_output_tokens"] = options.MaxOutputTokens
                };
                AddReasoningEffort(body);

                var response = await SendAndParseAsync(body, cancellationToken).ConfigureAwait(false);
                var output = response["output"] as JArray ?? new JArray();
                var functionCalls = output.OfType<JObject>().ToList();
                var foundFunctionCall = false;

                // Responses models may return reasoning and message items alongside
                // function calls. Preserve the complete output when continuing the
                // manual function-call loop, as required by the Responses contract.
                foreach (var item in functionCalls)
                    input.Add(item.DeepClone());

                foreach (var item in functionCalls)
                {
                    if (!string.Equals(item["type"]?.ToString(), "function_call", StringComparison.Ordinal))
                        continue;

                    foundFunctionCall = true;
                    var name = item["name"]?.ToString();
                    var callId = item["call_id"]?.ToString();
                    var result = toolServer.CallTool(name, ParseArguments(item["arguments"]?.ToString()));
                    if (!result.Success && !result.IsQuery)
                        commands.Add($"# {name}: {result.Text}");
                    else if (!string.IsNullOrWhiteSpace(result.Command))
                        commands.Add(result.Command);

                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = result.Text ?? string.Empty
                    });
                }

                if (!foundFunctionCall)
                {
                    if (commands.Count > 0)
                        return string.Join(Environment.NewLine, commands);

                    var text = GetResponsesText(response);
                    return string.IsNullOrWhiteSpace(text)
                        ? "# No commands generated"
                        : $"# AI Response (no function calls):{Environment.NewLine}{text}";
                }
            }

            return commands.Count == 0
                ? "# Tool loop stopped after reaching the safety limit"
                : string.Join(Environment.NewLine, commands);
        }

        private async Task<JObject> SendAndParseAsync(JObject body, CancellationToken cancellationToken)
        {
            using (var response = await SendAsync(body, cancellationToken).ConfigureAwait(false))
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new OpenAICompatibleApiException(
                        $"AI API error: {(int)response.StatusCode} {response.ReasonPhrase}",
                        response.StatusCode,
                        content);
                }

                try
                {
                    return JObject.Parse(content);
                }
                catch (JsonException ex)
                {
                    throw new OpenAICompatibleApiException(
                        $"AI API returned invalid JSON: {ex.Message}",
                        response.StatusCode,
                        content,
                        ex);
                }
            }
        }

        private async Task<HttpResponseMessage> SendAsync(JObject body, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpointUrl());
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {options.ApiKey}");

            request.Content = new StringContent(
                body.ToString(Formatting.None),
                Encoding.UTF8,
                "application/json");

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(options.Timeout);
                return await HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            }
        }

        private async Task<HttpResponseMessage> SendGetAsync(string url, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {options.ApiKey}");

                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(options.Timeout);
                    return await HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private string BuildEndpointUrl()
        {
            var baseUrl = (options.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var endpointName = options.Protocol == AIEndpointProtocol.Responses
                ? "responses"
                : "chat/completions";

            if (baseUrl.EndsWith("/responses", StringComparison.OrdinalIgnoreCase) ||
                baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return baseUrl;
            }

            return $"{baseUrl}/{endpointName}";
        }

        private string BuildModelsEndpointUrl()
        {
            var baseUrl = (options.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
                return baseUrl;

            if (baseUrl.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - "/responses".Length);
            else if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - "/chat/completions".Length);

            return $"{baseUrl}/models";
        }

        private void AddReasoningEffort(JObject body)
        {
            if (!string.IsNullOrWhiteSpace(options.ReasoningEffort))
                body["reasoning_effort"] = options.ReasoningEffort.Trim().ToLowerInvariant();
        }

        private void ValidateConfiguration()
        {
            ValidateEndpoint();
            if (string.IsNullOrWhiteSpace(options.Model))
                throw new InvalidOperationException("An AI model is required.");
            if (options.MaxToolTurns < 1 || options.MaxToolTurns > 200)
                throw new InvalidOperationException("MaxToolTurns must be between 1 and 200.");
        }

        private void ValidateEndpoint()
        {
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException("An AI API base URL is required.");
        }

        private static JObject ParseArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return new JObject();

            try
            {
                return JObject.Parse(arguments);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Tool arguments were not valid JSON: {ex.Message}", ex);
            }
        }

        private static string GetResponsesText(JObject response)
        {
            var outputText = response["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText))
                return outputText;

            var textParts = new List<string>();
            foreach (var item in (response["output"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                if (!string.Equals(item["type"]?.ToString(), "message", StringComparison.Ordinal))
                    continue;

                foreach (var content in (item["content"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var text = content["text"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        textParts.Add(text);
                }
            }

            return string.Join(Environment.NewLine, textParts);
        }
    }

    public sealed class OpenAICompatibleApiException : Exception
    {
        public OpenAICompatibleApiException(string message, System.Net.HttpStatusCode statusCode, string responseBody, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public System.Net.HttpStatusCode StatusCode { get; }
        public string ResponseBody { get; }
    }
}
