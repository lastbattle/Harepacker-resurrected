using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Settings for AI integration with persistent storage
    /// </summary>
    public static class AISettings
    {
        private const string DEFAULT_MODEL = "google/gemini-3-flash-preview";

        private static string _apiKey = string.Empty;
        private static string _model = DEFAULT_MODEL;
        private static bool _loaded = false;

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HaCreator", "Settings_AI.json");

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

        public static bool IsConfigured
        {
            get
            {
                EnsureLoaded();
                return !string.IsNullOrWhiteSpace(_apiKey);
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
                    _apiKey = settings["apiKey"]?.ToString() ?? string.Empty;
                    _model = settings["model"]?.ToString() ?? DEFAULT_MODEL;
                }
            }
            catch
            {
                // Ignore load errors
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
                    ["apiKey"] = _apiKey,
                    ["model"] = _model
                };
                File.WriteAllText(SettingsFilePath, settings.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
