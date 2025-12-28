/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Client for OpenRouter API to process natural language map editing instructions.
    /// Uses function calling for structured output.
    /// </summary>
    public class OpenRouterClient
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_URL = "https://openrouter.ai/api/v1/chat/completions";

        private readonly string apiKey;
        private readonly string model;

        public OpenRouterClient(string apiKey, string model = "google/gemini-2.0-flash-001")
        {
            this.apiKey = apiKey;
            this.model = model;
        }

        /// <summary>
        /// Process natural language instructions using function calling.
        /// </summary>
        /// <param name="mapContext">The current map state in AI-readable format</param>
        /// <param name="userInstructions">Natural language instructions from the user</param>
        /// <returns>List of executable map commands</returns>
        public async Task<string> ProcessInstructionsAsync(string mapContext, string userInstructions)
        {
            var systemPrompt = LoadSystemPrompt();
            var userMessage = $@"## Current Map State
{mapContext}

## User Request
{userInstructions}

Use the available functions to fulfill the user's request. Call multiple functions as needed.";

            var requestBody = new JObject
            {
                ["model"] = this.model,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = userMessage }
                },
                ["tools"] = MapEditorFunctions.GetToolDefinitions(),
                ["tool_choice"] = "required",  // Force function calling
                ["temperature"] = 0.2,  // Lower temperature for more consistent output
                ["max_tokens"] = 4000
            };

            var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(
                requestBody.ToString(Formatting.None),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenRouter API error: {response.StatusCode} - {responseContent}");
            }

            var jsonResponse = JObject.Parse(responseContent);
            return ParseFunctionCallResponse(jsonResponse);
        }

        /// <summary>
        /// Parse the function call response and convert to command strings
        /// </summary>
        private string ParseFunctionCallResponse(JObject response)
        {
            var commands = new List<string>();
            var message = response["choices"]?[0]?["message"];

            if (message == null)
                return "# No response from AI";

            // Check for tool calls
            var toolCalls = message["tool_calls"] as JArray;
            if (toolCalls != null && toolCalls.Count > 0)
            {
                foreach (var toolCall in toolCalls)
                {
                    var function = toolCall["function"];
                    if (function != null)
                    {
                        var functionName = function["name"]?.ToString();
                        var argumentsStr = function["arguments"]?.ToString();

                        if (!string.IsNullOrEmpty(functionName) && !string.IsNullOrEmpty(argumentsStr))
                        {
                            try
                            {
                                var arguments = JObject.Parse(argumentsStr);
                                var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);
                                commands.Add(command);
                            }
                            catch (Exception ex)
                            {
                                commands.Add($"# Error parsing {functionName}: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // If no tool calls, check for regular content (fallback)
            if (commands.Count == 0)
            {
                var content = message["content"]?.ToString();
                if (!string.IsNullOrEmpty(content))
                {
                    // The AI might have responded with text instead of function calls
                    commands.Add("# AI Response (no function calls):");
                    commands.Add(content);
                }
                else
                {
                    commands.Add("# No commands generated");
                }
            }

            return string.Join(Environment.NewLine, commands);
        }

        /// <summary>
        /// Load the system prompt from external file or use embedded default
        /// </summary>
        private string LoadSystemPrompt()
        {
            // Try to load from external file first
            var externalPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                "AI", "Prompts", "MapEditorSystemPrompt.txt");

            if (File.Exists(externalPath))
            {
                try
                {
                    return File.ReadAllText(externalPath);
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Try source location
            var sourcePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "MapEditor", "AI", "Prompts", "MapEditorSystemPrompt.txt");

            if (File.Exists(sourcePath))
            {
                try
                {
                    return File.ReadAllText(sourcePath);
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Default embedded prompt
            return GetDefaultSystemPrompt();
        }

        private string GetDefaultSystemPrompt()
        {
            return @"You are a MapleStory map editor assistant. Your job is to convert natural language instructions into map editing function calls.

You have access to functions to modify the map. Use them to fulfill the user's requests.

## Important Guidelines

1. Coordinate System:
   - X increases to the right, decreases to the left
   - Y increases downward (so 'top' means smaller Y, 'bottom' means larger Y)
   - The center point is typically (0, 0) or near it

2. Common Mob IDs:
   - 100100 = Blue Snail
   - 100101 = Red Snail
   - 100110 = Shroom
   - 100120 = Stump
   - 1210100 = Slime

3. Common NPC IDs:
   - 9000000 = Maple Administrator
   - 9010000 = Henesys NPC

4. Portal Types: StartPoint, Visible, Hidden, Script, Collision

5. When the user says:
   - 'left side' = negative X or X < center
   - 'right side' = positive X or X > center
   - 'top' = smaller Y values
   - 'bottom' = larger Y values
   - 'a few' = 2-3, 'some' = 3-5, 'many' = 5-8

6. Space mobs apart by 100-200 pixels for good gameplay.

Always use the current map state to determine appropriate positions.";
        }

        /// <summary>
        /// Test if the API key is valid
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Simple test without function calling
                var requestBody = new JObject
                {
                    ["model"] = this.model,
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "user", ["content"] = "Say OK" }
                    },
                    ["max_tokens"] = 10
                };

                var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(
                    requestBody.ToString(Formatting.None),
                    Encoding.UTF8,
                    "application/json");

                var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

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
                File.WriteAllText(SettingsFilePath, settings.ToString(Formatting.Indented));
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
