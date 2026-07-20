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
        private const string DefaultModel = "google/gemini-3-flash-preview";
        private const string DefaultImageModel = "gpt-image-2";
        private const AIEndpointProtocol DefaultProtocol = AIEndpointProtocol.ChatCompletions;

        private static string apiKey = string.Empty;
        private static string baseUrl = DefaultBaseUrl;
        private static string model = DefaultModel;
        private static string imageModel = DefaultImageModel;
        private static AIEndpointProtocol protocol = DefaultProtocol;
        private static string reasoningEffort = string.Empty;
        private static bool strictSchemas;
        private static bool autoApplyCommands = true;
        private static int maxToolTurns = 40;
        private static int maxOutputTokens = 100000;
        private static bool loaded;

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HaCreator", "Settings_AI.json");

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
            "google/gemini-3.1-flash-lite-preview",
            "google/gemini-3.1-pro-preview",
            "openai/gpt-5.4",
            "openai/gpt-5.3-codex",
            "anthropic/claude-sonnet-4.5",
            "anthropic/claude-opus-4.5"
        };

        public static readonly string[] AvailableImageModels =
        {
            DefaultImageModel,
            "gpt-image-1.5"
        };

        public static readonly string[] AvailableReasoningEfforts =
        {
            "low", "medium", "high", "xhigh"
        };

        public static OpenAICompatibleOptions CreateOptions()
        {
            EnsureLoaded();
            return new OpenAICompatibleOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                Model = model,
                Protocol = protocol,
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
