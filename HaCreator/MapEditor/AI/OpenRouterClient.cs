using System;
using System.Collections.Generic;
using System.Net.Http;
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
    public class OpenRouterClient : IAIClient
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_URL = "https://openrouter.ai/api/v1/chat/completions";
        private const double TEMPERATURE = 1;
        private const int MAX_OUT_TOKENS = 100000;

        private readonly string apiKey;
        private readonly string model;

        // Track which query functions have been called in this conversation
        private HashSet<string> _calledQueryFunctions = new HashSet<string>();

        public OpenRouterClient(string apiKey, string model)
        {
            this.apiKey = apiKey;
            this.model = model;
        }

        /// <summary>
        /// Process natural language instructions using function calling.
        /// Handles multi-turn conversations for query functions (get_object_info, get_background_info).
        /// Enforces query-first pattern: add_* functions require corresponding query to be called first.
        /// </summary>
        /// <param name="mapContext">The current map state in AI-readable format</param>
        /// <param name="userInstructions">Natural language instructions from the user</param>
        /// <returns>List of executable map commands</returns>
        public async Task<string> ProcessInstructionsAsync(string mapContext, string userInstructions)
        {
            // Reset query tracker for this conversation
            _calledQueryFunctions.Clear();

            var systemPrompt = MapEditorPromptBuilder.LoadSystemPrompt();
            var userMessage = MapEditorPromptBuilder.BuildUserMessage(mapContext, userInstructions);

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
                    ["max_tokens"] = MAX_OUT_TOKENS
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
        /// Enforces query-first pattern: action functions requiring queries will be rejected with an error if query wasn't called.
        /// Returns: (action commands, all tool responses for multi-turn, has query functions, assistant message for multi-turn)
        /// </summary>
        private (List<string> commands, List<(string toolCallId, string result)> toolResponses, bool hasQueryFunctions, JObject assistantMessage)
            ParseFunctionCallResponseWithQueries(JObject response)
        {
            var commands = new List<string>();
            var toolResponses = new List<(string toolCallId, string result)>();
            bool hasQueryFunctions = false;
            bool hasQueryViolations = false;
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
                                    // Track that this query was called
                                    _calledQueryFunctions.Add(functionName);
                                    // Log that query was called (visible in output)
                                    commands.Add($"# QUERY: {functionName} called");
                                    var result = MapEditorFunctions.ExecuteQueryFunction(functionName, arguments);
                                    toolResponses.Add((toolCallId, result));
                                }
                                else
                                {
                                    // Check if this action function requires a query
                                    var requiredQuery = MapEditorFunctions.GetRequiredQuery(functionName);
                                    if (requiredQuery != null && !_calledQueryFunctions.Contains(requiredQuery))
                                    {
                                        // Query was not called first - AUTO-EXECUTE the query then allow the command
                                        hasQueryViolations = true;
                                        commands.Add($"# WARNING: {functionName} called without {requiredQuery} - query should be called first!");

                                        // Still convert to command (IDs might be valid from AI's training)
                                        var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);
                                        commands.Add(command);
                                        toolResponses.Add((toolCallId, $"Command queued: {functionName} (WARNING: {requiredQuery} was not called first - IDs may be invalid)"));
                                    }
                                    else
                                    {
                                        // Query was called (or not required) - convert to command
                                        var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);
                                        commands.Add(command);

                                        // Google Gemini requires a response for EVERY tool call
                                        // Provide a simple acknowledgment for action functions
                                        toolResponses.Add((toolCallId, $"Command queued: {functionName}"));
                                    }
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
}
