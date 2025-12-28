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
using MapleLib.WzLib.WzStructure.Data;
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
        private const double TEMPERATURE = 1;
        private const int MAX_TOKENS = 200000;

        private readonly string apiKey;
        private readonly string model;

        public OpenRouterClient(string apiKey, string model = "google/gemini-3-flash-preview")
        {
            this.apiKey = apiKey;
            this.model = model;
        }

        /// <summary>
        /// Process natural language instructions using function calling.
        /// Handles multi-turn conversations for query functions (get_object_info, get_background_info).
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

Use the available functions to fulfill the user's request. Call multiple functions as needed.
IMPORTANT: For objects and backgrounds, call get_object_info or get_background_info FIRST to discover valid paths, then use add_object or add_background with those exact paths.";

            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = userMessage }
            };

            var allCommands = new List<string>();
            int maxTurns = 40; // Limit iterations for safety

            for (int turn = 0; turn < maxTurns; turn++)
            {
                var requestBody = new JObject
                {
                    ["model"] = this.model,
                    ["messages"] = messages,
                    ["tools"] = MapEditorFunctions.GetToolDefinitions(),
                    ["tool_choice"] = turn == 0 ? "required" : "auto", // First turn requires tools, subsequent are optional
                    ["temperature"] = TEMPERATURE,
                    ["max_tokens"] = MAX_TOKENS
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
                var (commands, toolResponses, hasQueryFunctions, assistantMessage) = ParseFunctionCallResponseWithQueries(jsonResponse);

                // Add all action commands to the result
                allCommands.AddRange(commands);

                // If there were query function calls, we need to continue the conversation
                // to let the AI use the query results to make action calls
                if (hasQueryFunctions && toolResponses.Count > 0)
                {
                    // Add the assistant's message with tool_calls
                    messages.Add(assistantMessage);

                    // Add tool results for EVERY function call (required by Google Gemini)
                    foreach (var (toolCallId, result) in toolResponses)
                    {
                        messages.Add(new JObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = toolCallId,
                            ["content"] = result
                        });
                    }

                    // Continue to next turn to let AI use the query results
                    continue;
                }

                // No query functions, we're done (action-only calls don't need follow-up)
                break;
            }

            if (allCommands.Count == 0)
            {
                return "# No commands generated";
            }

            return string.Join(Environment.NewLine, allCommands);
        }

        /// <summary>
        /// Parse the function call response, separating query functions from action functions.
        /// Returns: (action commands, all tool responses for multi-turn, has query functions, assistant message for multi-turn)
        /// </summary>
        private (List<string> commands, List<(string toolCallId, string result)> toolResponses, bool hasQueryFunctions, JObject assistantMessage)
            ParseFunctionCallResponseWithQueries(JObject response)
        {
            var commands = new List<string>();
            var toolResponses = new List<(string toolCallId, string result)>();
            bool hasQueryFunctions = false;
            var message = response["choices"]?[0]?["message"] as JObject;

            if (message == null)
                return (new List<string> { "# No response from AI" }, toolResponses, false, null);

            // Check for tool calls
            var toolCalls = message["tool_calls"] as JArray;
            if (toolCalls != null && toolCalls.Count > 0)
            {
                foreach (var toolCall in toolCalls)
                {
                    var toolCallId = toolCall["id"]?.ToString();
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

                                // Check if this is a query function
                                if (MapEditorFunctions.IsQueryFunction(functionName))
                                {
                                    hasQueryFunctions = true;
                                    var result = MapEditorFunctions.ExecuteQueryFunction(functionName, arguments);
                                    toolResponses.Add((toolCallId, result));
                                }
                                else
                                {
                                    // Regular action function - convert to command
                                    var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);
                                    commands.Add(command);

                                    // Google Gemini requires a response for EVERY tool call
                                    // Provide a simple acknowledgment for action functions
                                    toolResponses.Add((toolCallId, $"Command queued: {functionName}"));
                                }
                            }
                            catch (Exception ex)
                            {
                                commands.Add($"# Error parsing {functionName}: {ex.Message}");
                                // Still need to respond to the tool call even on error
                                if (!string.IsNullOrEmpty(toolCallId))
                                {
                                    toolResponses.Add((toolCallId, $"Error: {ex.Message}"));
                                }
                            }
                        }
                    }
                }
            }

            // If no tool calls at all, check for regular content (fallback)
            if (commands.Count == 0 && toolResponses.Count == 0)
            {
                var content = message["content"]?.ToString();
                if (!string.IsNullOrEmpty(content))
                {
                    commands.Add("# AI Response (no function calls):");
                    commands.Add(content);
                }
            }

            return (commands, toolResponses, hasQueryFunctions, message);
        }

        /// <summary>
        /// Load the system prompt from external file
        /// </summary>
        private string LoadSystemPrompt()
        {
            string promptContent = null;

            // Try to load from external file first
            var externalPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                "AI", "Prompts", "MapEditorSystemPrompt.txt");

            if (File.Exists(externalPath))
            {
                promptContent = File.ReadAllText(externalPath);
            }
            else
            {
                // Try source location
                var sourcePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "MapEditor", "AI", "Prompts", "MapEditorSystemPrompt.txt");

                if (File.Exists(sourcePath))
                {
                    promptContent = File.ReadAllText(sourcePath);
                }
                else
                {
                    throw new FileNotFoundException(
                        $"System prompt file not found. Expected at:\n- {externalPath}\n- {sourcePath}");
                }
            }

            // Replace dynamic placeholders
            promptContent = promptContent.Replace("{PORTAL_TYPES}", GeneratePortalTypesDocumentation());

            return promptContent;
        }

        /// <summary>
        /// Generate portal types documentation dynamically from PortalType enum
        /// </summary>
        private string GeneratePortalTypesDocumentation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Portal types control how the portal appears and behaves:");
            sb.AppendLine();

            foreach (PortalType portalType in Enum.GetValues(typeof(PortalType)))
            {
                try
                {
                    var code = portalType.ToCode();
                    var friendlyName = portalType.GetFriendlyName();
                    sb.AppendLine($"- `{portalType}` ({code}) = {friendlyName}");
                }
                catch
                {
                    // Skip if extension methods fail for any reason
                }
            }

            return sb.ToString().TrimEnd();
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
