/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
        private const string DEFAULT_MODEL = "google/gemini-2.0-flash-001";

        private static string _apiKey = string.Empty;
        private static string _model = DEFAULT_MODEL;
        private static bool _loaded = false;

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HaCreator", "ai_settings.json");

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
            DEFAULT_MODEL
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
