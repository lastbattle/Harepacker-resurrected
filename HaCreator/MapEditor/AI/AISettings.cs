using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Persistent settings for the OpenAI-compatible AI endpoint.
    /// The OpenRouter URL is only a default preset; custom RPC endpoints use the same settings.
    /// </summary>
    public static class AISettings
    {
        private const string DefaultBaseUrl = "https://openrouter.ai/api/v1";
        private const string DefaultModel = "openai/gpt-6-astra";
        private const string DefaultImageModel = "gpt-image-2";
        private const AIEndpointProtocol DefaultProtocol = AIEndpointProtocol.Responses;

        private static string apiKey = string.Empty;
        private static string baseUrl = DefaultBaseUrl;
        private static string model = DefaultModel;
        private static string imageModel = DefaultImageModel;
        private static AIEndpointProtocol protocol = DefaultProtocol;
        private static string reasoningEffort = string.Empty;
        private static readonly System.Collections.Generic.Dictionary<string, string> reasoningEffortsByModel =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool strictSchemas;
        private static bool autoApplyCommands = true;
        private static int maxToolTurns = 40;
        private static int maxOutputTokens = 100000;
        private static bool loaded;

        private static readonly string SettingsFilePath =
            HaSharedLibrary.Configuration.UserDataPaths.HaCreatorAiSettingsFile;

        public static AIProvider Provider
        {
            get
            {
                EnsureLoaded();
                return AIProvider.OpenAICompatible;
            }
            set
            {
                // Kept for compatibility with callers written before endpoint presets existed.
                EnsureLoaded();
                Save();
            }
        }

        public static bool IsConfigured
        {
            get
            {
                EnsureLoaded();
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
                    return false;

                // Cloud endpoints need a key. Local/dev endpoints may authenticate elsewhere.
                if (!string.IsNullOrWhiteSpace(apiKey))
                    return true;

                return IsLocalEndpoint(baseUrl);
            }
        }

        public static string ApiKey
        {
            get { EnsureLoaded(); return apiKey; }
            set { apiKey = value ?? string.Empty; Save(); }
        }

        public static string BaseUrl
        {
            get { EnsureLoaded(); return baseUrl; }
            set { baseUrl = string.IsNullOrWhiteSpace(value) ? DefaultBaseUrl : value.Trim(); Save(); }
        }

        public static string Model
        {
            get { EnsureLoaded(); return model; }
            set { model = string.IsNullOrWhiteSpace(value) ? DefaultModel : value.Trim(); Save(); }
        }

        /// <summary>
        /// Model used by OpenAI-compatible image generation and edit endpoints.
        /// This is intentionally independent from the text/tool model.
        /// </summary>
        public static string ImageModel
        {
            get { EnsureLoaded(); return imageModel; }
            set { imageModel = string.IsNullOrWhiteSpace(value) ? DefaultImageModel : value.Trim(); Save(); }
        }

        public static AIEndpointProtocol Protocol
        {
            get { EnsureLoaded(); return protocol; }
            set { protocol = value; Save(); }
        }

        public static string ReasoningEffort
        {
            get { EnsureLoaded(); return reasoningEffort; }
            set
            {
                var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
                reasoningEffort = AvailableReasoningEfforts.Contains(normalized) ? normalized : string.Empty;
                Save();
            }
        }

        public static string GetReasoningEffortForModel(string modelId)
        {
            EnsureLoaded();
            if (!string.IsNullOrWhiteSpace(modelId) && reasoningEffortsByModel.TryGetValue(modelId.Trim(), out var value))
                return value;
            return string.Equals(modelId, model, StringComparison.OrdinalIgnoreCase) ? reasoningEffort : string.Empty;
        }

        public static void SetReasoningEffortForModel(string modelId, string value)
        {
            EnsureLoaded();
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (!AvailableReasoningEfforts.Contains(normalized))
                normalized = string.Empty;
            if (!string.IsNullOrWhiteSpace(modelId))
                reasoningEffortsByModel[modelId.Trim()] = normalized;
            if (string.Equals(modelId, model, StringComparison.OrdinalIgnoreCase))
                reasoningEffort = normalized;
            Save();
        }

        public static bool StrictSchemas
        {
            get { EnsureLoaded(); return strictSchemas; }
            set { strictSchemas = value; Save(); }
        }

        /// <summary>
        /// Apply valid commands returned by an AI turn immediately after the response.
        /// This is enabled by default so a prompt can create or edit a map autonomously.
        /// </summary>
        public static bool AutoApplyCommands
        {
            get { EnsureLoaded(); return autoApplyCommands; }
            set { autoApplyCommands = value; Save(); }
        }

        public static int MaxToolTurns
        {
            get { EnsureLoaded(); return maxToolTurns; }
            set { maxToolTurns = Math.Max(1, Math.Min(200, value)); Save(); }
        }

        public static int MaxOutputTokens
        {
            get { EnsureLoaded(); return maxOutputTokens; }
            set { maxOutputTokens = Math.Max(256, Math.Min(1000000, value)); Save(); }
        }

        public static readonly string[] AvailableModels =
        {
            DefaultModel,
            "openai/gpt-5.6-sol",
            "openai/gpt-5.6-terra",
            "openai/gpt-5.6-luna",
            "anthropic/claude-opus-5",
            "anthropic/claude-opus-4.8",
            "anthropic/claude-sonnet-5",
            "meta/muse-spark-1.2",
            "x-ai/grok-4.5",
            "z-ai/glm-5.2",
            "~deepseek/deepseek-v4-flash-latest",
            "deepseek/deepseek-v4-pro",
            "google/gemini-3.6-flash",
            "moonshotai/kimi-k3",
        };

        public static readonly string[] AvailableImageModels =
        {
            DefaultImageModel,
            "gpt-image-1.5"
        };

        public static readonly string[] AvailableReasoningEfforts =
        {
            "minimal", "low", "medium", "high", "xhigh", "max", "ultra"
        };

        public static bool IsAstraModel(string modelId)
        {
            var id = modelId?.Trim();
            return string.Equals(id, "gpt-6-astra", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "openai/gpt-6-astra", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeModelId(string endpoint, string modelId)
        {
            var id = modelId?.Trim() ?? string.Empty;
            if (!IsAstraModel(id)) return id;
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
                uri.Host.Equals("openrouter.ai", StringComparison.OrdinalIgnoreCase)
                ? "openai/gpt-6-astra" : "gpt-6-astra";
        }

        public static readonly string[] AstraReasoningEfforts =
            { "low", "medium", "high", "xhigh", "max", "ultra" };

        public static OpenAICompatibleOptions CreateMapEditorOptions()
        {
            var options = CreateOptions();
            // Map editing is designed and validated for Astra's multimodal Responses loop.
            // Keep the user's endpoint/key and permit endpoint-qualified Astra model IDs.
            if (!options.Model.Contains("gpt-6-astra", StringComparison.OrdinalIgnoreCase))
            {
                options.Model = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
                    uri.Host.Equals("openrouter.ai", StringComparison.OrdinalIgnoreCase)
                    ? "openai/gpt-6-astra" : "gpt-6-astra";
                options.ReasoningEffort = "low";
            }
            if (string.IsNullOrWhiteSpace(options.ReasoningEffort)) options.ReasoningEffort = "low";
            options.Protocol = AIEndpointProtocol.Responses;
            options.MaxOutputTokens = Math.Min(options.MaxOutputTokens, 16000);
            return options;
        }

        public static OpenAICompatibleOptions CreateOptions()
        {
            EnsureLoaded();
            return new OpenAICompatibleOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                Model = NormalizeModelId(baseUrl, model),
                Protocol = IsAstraModel(model) ? AIEndpointProtocol.Responses : protocol,
                ReasoningEffort = reasoningEffort,
                StrictSchemas = strictSchemas,
                MaxToolTurns = maxToolTurns,
                MaxOutputTokens = maxOutputTokens
            };
        }

        public static string GetProviderDisplayName()
        {
            return "OpenAI-compatible API";
        }

        public static string GetProviderDisplayName(AIProvider provider)
        {
            return GetProviderDisplayName();
        }

        public static string GetActiveModel()
        {
            return Model;
        }

        public static string GetStatusDescription()
        {
            EnsureLoaded();
            return string.IsNullOrWhiteSpace(apiKey)
                ? $"{protocol} ({model}, key not set)"
                : $"{protocol} ({model}) @ {baseUrl}";
        }

        /// <summary>
        /// True when the failure is likely fixed by correcting endpoint/key settings.
        /// </summary>
        public static bool IsConfigurationRelatedError(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (TryGetStatusCode(current, out var statusCode) &&
                    IsConfigurationStatusCode(statusCode))
                {
                    return true;
                }

                if (current is HttpRequestException ||
                    current is UriFormatException ||
                    current is WebException)
                {
                    return true;
                }

                if (current is InvalidOperationException &&
                    ContainsConfigurationHint(current.Message))
                {
                    return true;
                }

                if (ContainsConfigurationHint(current.Message))
                    return true;
            }

            return false;
        }

        public static bool IsLocalEndpoint(string candidateBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(candidateBaseUrl))
                return false;

            if (!Uri.TryCreate(candidateBaseUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            if (uri.IsLoopback)
                return true;

            var host = uri.Host;
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetStatusCode(Exception ex, out HttpStatusCode statusCode)
        {
            statusCode = default;

            if (ex is OpenAICompatibleApiException apiEx)
            {
                statusCode = apiEx.StatusCode;
                return true;
            }

            var property = ex.GetType().GetProperty("StatusCode", BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return false;

            var value = property.GetValue(ex);
            if (value is HttpStatusCode code)
            {
                statusCode = code;
                return true;
            }

            if (value is HttpStatusCode?)
            {
                var nullable = (HttpStatusCode?)value;
                if (!nullable.HasValue)
                    return false;

                statusCode = nullable.Value;
                return true;
            }

            if (value is int numeric)
            {
                statusCode = (HttpStatusCode)numeric;
                return true;
            }

            return false;
        }

        private static bool IsConfigurationStatusCode(HttpStatusCode statusCode)
        {
            var code = (int)statusCode;
            return statusCode == HttpStatusCode.Unauthorized ||
                   statusCode == HttpStatusCode.Forbidden ||
                   statusCode == HttpStatusCode.NotFound ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout ||
                   code == 418 ||
                   code == 421;
        }

        private static bool ContainsConfigurationHint(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var text = message.ToLowerInvariant();
            return text.Contains("api key") ||
                   text.Contains("apikey") ||
                   text.Contains("unauthorized") ||
                   text.Contains("authentication") ||
                   text.Contains("authorization") ||
                   text.Contains("invalid token") ||
                   text.Contains("base url") ||
                   text.Contains("endpoint") ||
                   text.Contains("model is required") ||
                   text.Contains("ai model is required") ||
                   text.Contains("image model is required") ||
                   text.Contains("could not resolve") ||
                   text.Contains("nodename nor servname") ||
                   text.Contains("no such host") ||
                   text.Contains("connection refused") ||
                   text.Contains("actively refused") ||
                   text.Contains("ssl connection") ||
                   text.Contains("certificate");
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            Load();
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return;

                var settings = JObject.Parse(File.ReadAllText(SettingsFilePath));
                apiKey = settings["apiKey"]?.ToString() ?? string.Empty;
                baseUrl = settings["baseUrl"]?.ToString() ?? DefaultBaseUrl;
                model = settings["model"]?.ToString() ?? DefaultModel;
                imageModel = settings["imageModel"]?.ToString() ?? DefaultImageModel;

                // Older files only stored OpenRouter settings. Preserve those values while
                // moving them to the provider-neutral endpoint model.
                if (settings["openRouterApiUrl"] != null && settings["baseUrl"] == null)
                    baseUrl = settings["openRouterApiUrl"].ToString();

                if (Enum.TryParse(settings["protocol"]?.ToString(), true, out AIEndpointProtocol parsedProtocol))
                    protocol = parsedProtocol;

                reasoningEffort = settings["reasoningEffort"]?.ToString() ?? string.Empty;
                if (settings["reasoningEffortsByModel"] is JObject perModel)
                {
                    foreach (var property in perModel.Properties())
                    {
                        var value = property.Value?.ToString()?.Trim().ToLowerInvariant();
                        if (AvailableReasoningEfforts.Contains(value))
                            reasoningEffortsByModel[property.Name] = value;
                    }
                }
                strictSchemas = settings["strictSchemas"]?.Value<bool>() ?? false;
                autoApplyCommands = settings["autoApplyCommands"]?.Value<bool>() ?? true;
                maxToolTurns = Clamp(settings["maxToolTurns"]?.Value<int>() ?? 40, 1, 200);
                maxOutputTokens = Clamp(settings["maxOutputTokens"]?.Value<int>() ?? 100000, 256, 1000000);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AISettings] Failed to load settings: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var settings = new JObject
                {
                    ["baseUrl"] = baseUrl,
                    ["apiKey"] = apiKey,
                    ["model"] = model,
                    ["imageModel"] = imageModel,
                    ["protocol"] = protocol.ToString(),
                    ["reasoningEffort"] = reasoningEffort,
                    ["reasoningEffortsByModel"] = JObject.FromObject(reasoningEffortsByModel),
                    ["strictSchemas"] = strictSchemas,
                    ["autoApplyCommands"] = autoApplyCommands,
                    ["maxToolTurns"] = maxToolTurns,
                    ["maxOutputTokens"] = maxOutputTokens
                };
                File.WriteAllText(SettingsFilePath, settings.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AISettings] Failed to save settings: {ex.Message}");
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
