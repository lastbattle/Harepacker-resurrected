using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Settings for AI integration with persistent storage.
    /// Supports multiple AI providers: OpenRouter and OpenCode.
    /// </summary>
    public static class AISettings
    {
        // OpenRouter defaults
        private const string DEFAULT_MODEL = "google/gemini-3-flash-preview";

        // OpenCode defaults
        private const string DEFAULT_OPENCODE_HOST = "127.0.0.1";
        private const int DEFAULT_OPENCODE_PORT = 4096;
        private const string DEFAULT_OPENCODE_MODEL = "claude-opus-4-5-20251101";//"claude-sonnet-4-5-20250929";

        // OpenRouter settings
        private static string _apiKey = string.Empty;
        private static string _model = DEFAULT_MODEL;

        // OpenCode settings
        private static string _openCodeHost = DEFAULT_OPENCODE_HOST;
        private static int _openCodePort = DEFAULT_OPENCODE_PORT;
        private static string _openCodeModel = DEFAULT_OPENCODE_MODEL;
        private static bool _openCodeAutoStart = true;

        // Provider selection
        private static AIProvider _provider = AIProvider.OpenRouter;

        private static bool _loaded = false;

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HaCreator", "Settings_AI.json");

        #region Provider Selection

        /// <summary>
        /// Currently selected AI provider
        /// </summary>
        public static AIProvider Provider
        {
            get
            {
                EnsureLoaded();
                return _provider;
            }
            set
            {
                _provider = value;
                Save();
            }
        }

        /// <summary>
        /// Check if the current provider is properly configured
        /// </summary>
        public static bool IsConfigured
        {
            get
            {
                EnsureLoaded();
                switch (_provider)
                {
                    case AIProvider.OpenCode:
                        // OpenCode doesn't require an API key (uses OAuth)
                        return !string.IsNullOrWhiteSpace(_openCodeHost) && _openCodePort > 0;
                    case AIProvider.OpenRouter:
                    default:
                        return !string.IsNullOrWhiteSpace(_apiKey);
                }
            }
        }

        #endregion

        #region OpenRouter Settings

        /// <summary>
        /// OpenRouter API key
        /// </summary>
        public static string ApiKey
        {
            get
            {
                EnsureLoaded();
                return _apiKey;
            }
            set
            {
                _apiKey = value ?? string.Empty;
                Save();
            }
        }

        /// <summary>
        /// OpenRouter model identifier
        /// </summary>
        public static string Model
        {
            get
            {
                EnsureLoaded();
                return _model;
            }
            set
            {
                _model = value ?? DEFAULT_MODEL;
                Save();
            }
        }

        /// <summary>
        /// Available models on OpenRouter that support function calling
        /// </summary>
        public static readonly string[] AvailableModels = new[]
        {
            DEFAULT_MODEL,
            "google/gemini-3-pro-preview",
            "openai/gpt-5.2",
            "anthropic/claude-sonnet-4.5",
            "anthropic/claude-opus-4.5"
        };

        #endregion

        #region OpenCode Settings

        /// <summary>
        /// OpenCode server hostname
        /// </summary>
        public static string OpenCodeHost
        {
            get
            {
                EnsureLoaded();
                return _openCodeHost;
            }
            set
            {
                _openCodeHost = string.IsNullOrWhiteSpace(value) ? DEFAULT_OPENCODE_HOST : value;
                Save();
            }
        }

        /// <summary>
        /// OpenCode server port
        /// </summary>
        public static int OpenCodePort
        {
            get
            {
                EnsureLoaded();
                return _openCodePort;
            }
            set
            {
                _openCodePort = value > 0 ? value : DEFAULT_OPENCODE_PORT;
                Save();
            }
        }

        /// <summary>
        /// OpenCode model identifier (e.g., "anthropic/claude-sonnet-4-20250514")
        /// </summary>
        public static string OpenCodeModel
        {
            get
            {
                EnsureLoaded();
                return _openCodeModel;
            }
            set
            {
                _openCodeModel = string.IsNullOrWhiteSpace(value) ? DEFAULT_OPENCODE_MODEL : value;
                Save();
            }
        }

        /// <summary>
        /// Available models for OpenCode (Claude models via OAuth)
        /// </summary>
        public static readonly string[] AvailableOpenCodeModels = new[]
        {
            DEFAULT_OPENCODE_MODEL,
            "claude-opus-4-5-20251101"
        };

        /// <summary>
        /// Whether to automatically start the OpenCode server if not running.
        /// When enabled, the client will launch 'opencode serve' automatically.
        /// </summary>
        public static bool OpenCodeAutoStart
        {
            get
            {
                EnsureLoaded();
                return _openCodeAutoStart;
            }
            set
            {
                _openCodeAutoStart = value;
                Save();
            }
        }

        #endregion

        #region Load/Save

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JObject.Parse(json);

                    // Load provider selection
                    var providerStr = settings["provider"]?.ToString();
                    if (Enum.TryParse<AIProvider>(providerStr, out var provider))
                    {
                        _provider = provider;
                    }

                    // Load OpenRouter settings
                    _apiKey = settings["apiKey"]?.ToString() ?? string.Empty;
                    _model = settings["model"]?.ToString() ?? DEFAULT_MODEL;

                    // Load OpenCode settings
                    _openCodeHost = settings["openCodeHost"]?.ToString() ?? DEFAULT_OPENCODE_HOST;
                    _openCodePort = settings["openCodePort"]?.Value<int>() ?? DEFAULT_OPENCODE_PORT;
                    _openCodeModel = settings["openCodeModel"]?.ToString() ?? DEFAULT_OPENCODE_MODEL;
                    _openCodeAutoStart = settings["openCodeAutoStart"]?.Value<bool>() ?? true;
                }
            }
            catch (Exception ex)
            {
                // Log load errors, use defaults
                System.Diagnostics.Debug.WriteLine($"[AISettings] Failed to load settings: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new JObject
                {
                    // Provider selection
                    ["provider"] = _provider.ToString(),

                    // OpenRouter settings
                    ["apiKey"] = _apiKey,
                    ["model"] = _model,

                    // OpenCode settings
                    ["openCodeHost"] = _openCodeHost,
                    ["openCodePort"] = _openCodePort,
                    ["openCodeModel"] = _openCodeModel,
                    ["openCodeAutoStart"] = _openCodeAutoStart
                };

                File.WriteAllText(SettingsFilePath, settings.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
                // Ignore save errors
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get the display name for the current provider
        /// </summary>
        public static string GetProviderDisplayName()
        {
            return GetProviderDisplayName(_provider);
        }

        /// <summary>
        /// Get the display name for a provider
        /// </summary>
        public static string GetProviderDisplayName(AIProvider provider)
        {
            switch (provider)
            {
                case AIProvider.OpenCode:
                    return "OpenCode (Local)";
                case AIProvider.OpenRouter:
                default:
                    return "OpenRouter";
            }
        }

        /// <summary>
        /// Get the currently configured model for the active provider
        /// </summary>
        public static string GetActiveModel()
        {
            EnsureLoaded();
            switch (_provider)
            {
                case AIProvider.OpenCode:
                    return _openCodeModel;
                case AIProvider.OpenRouter:
                default:
                    return _model;
            }
        }

        /// <summary>
        /// Get a status description for the current configuration
        /// </summary>
        public static string GetStatusDescription()
        {
            EnsureLoaded();
            switch (_provider)
            {
                case AIProvider.OpenCode:
                    return $"OpenCode @ {_openCodeHost}:{_openCodePort}";
                case AIProvider.OpenRouter:
                default:
                    return string.IsNullOrWhiteSpace(_apiKey)
                        ? "OpenRouter (not configured)"
                        : $"OpenRouter ({_model})";
            }
        }

        #endregion
    }
}
