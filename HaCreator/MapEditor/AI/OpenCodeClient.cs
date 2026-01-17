using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Client for OpenCode API to process natural language map editing instructions.
    /// OpenCode is an open-source AI coding agent that provides a REST API
    /// for programmatic access to AI models (including Claude via OAuth).
    /// Supports auto-starting the OpenCode server if not running.
    /// </summary>
    public class OpenCodeClient : IAIClient
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private const int DEFAULT_PORT = 4096;
        private const string DEFAULT_HOST = "127.0.0.1";
        private const int HEALTH_CHECK_TIMEOUT_MS = 5000;
        private const int REQUEST_TIMEOUT_MS = 300000; // 5 minutes
        private const int MAX_OUTPUT_TOKENS = 16000;
        private const int SERVER_START_TIMEOUT_MS = 30000; // 30 seconds to start

        private readonly string host;
        private readonly int port;
        private readonly string model;
        private readonly string baseUrl;
        private readonly bool autoStart;

        // Track the server process if we started it
        private static Process _serverProcess;
        private static readonly object _serverLock = new object();

        // AI Tool Server for handling server-side tool callbacks from OpenCode
        private static AIToolServer _toolServer;
        private static readonly object _toolServerLock = new object();

        // Track which query functions have been called in this conversation
        private HashSet<string> _calledQueryFunctions = new HashSet<string>();

        // Collect commands received during a session for UI display
        private static List<string> _collectedCommands = new List<string>();
        private static readonly object _commandsLock = new object();

        /// <summary>
        /// Event raised when a command is received from OpenCode's server-side tools.
        /// The orchestrator should subscribe to this to execute commands.
        /// </summary>
        public static event EventHandler<string> OnToolCommandReceived;

        /// <summary>
        /// Clear collected commands (call before starting a new request)
        /// </summary>
        public static void ClearCollectedCommands()
        {
            lock (_commandsLock)
            {
                _collectedCommands.Clear();
            }
        }

        /// <summary>
        /// Get all commands collected during the current session
        /// </summary>
        public static List<string> GetCollectedCommands()
        {
            lock (_commandsLock)
            {
                return new List<string>(_collectedCommands);
            }
        }

        /// <summary>
        /// Event raised when server status changes (starting, started, failed, stopped)
        /// </summary>
        public static event Action<string> OnServerStatusChanged;

        /// <summary>
        /// Initialize OpenCode client.
        /// </summary>
        /// <param name="host">Server hostname (default: 127.0.0.1)</param>
        /// <param name="port">Server port (default: 4096)</param>
        /// <param name="model">Model to use (e.g., "anthropic/claude-sonnet-4-20250514")</param>
        /// <param name="autoStart">Whether to auto-start the server if not running (default: true)</param>
        public OpenCodeClient(string host = null, int port = 0, string model = null, bool autoStart = true)
        {
            this.host = string.IsNullOrEmpty(host) ? DEFAULT_HOST : host;
            this.port = port > 0 ? port : DEFAULT_PORT;
            this.model = model;
            this.autoStart = autoStart;
            this.baseUrl = $"http://{this.host}:{this.port}";

            // Auto-regenerate TypeScript tools if C# definitions have changed
            try
            {
                OpenCodeToolGenerator.AutoRegenerateIfNeeded(null);
            }
            catch (Exception ex)
            {
                // Silently ignore - tools may not be needed or WZ data not loaded yet
                Debug.WriteLine($"[OpenCode] Auto-regenerate skipped: {ex.Message}");
            }

            // Start the AI Tool Server so OpenCode's server-side tools can call back to HaCreator
            StartToolServer();
        }

        /// <summary>
        /// Check if OpenCode server is running and healthy.
        /// Uses /config endpoint as health check since OpenCode doesn't have a dedicated /health endpoint.
        /// </summary>
        public async Task<bool> IsServerRunningAsync()
        {
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(HEALTH_CHECK_TIMEOUT_MS))
                {
                    // OpenCode doesn't have a /health endpoint - use /config (returns JSON object if server is running)
                    var response = await httpClient.GetAsync($"{baseUrl}/config", cts.Token);
                    if (!response.IsSuccessStatusCode)
                        return false;

                    // Verify it returns valid JSON (not HTML)
                    var content = await response.Content.ReadAsStringAsync();
                    return content.TrimStart().StartsWith("{");
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Start the OpenCode server if not already running.
        /// </summary>
        /// <returns>True if server started successfully or was already running</returns>
        public async Task<bool> StartServerAsync()
        {
            if (await IsServerRunningAsync())
            {
                OnServerStatusChanged?.Invoke("Server already running");
                return true;
            }

            lock (_serverLock)
            {
                // Double-check inside lock
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    return true;
                }

                // Check if Node.js is installed (required for OpenCode)
                if (!IsNodeInstalled())
                {
                    OnServerStatusChanged?.Invoke("Node.js is not installed. OpenCode requires Node.js to run. Please install from https://nodejs.org/");
                    return false;
                }

                OnServerStatusChanged?.Invoke("Starting OpenCode server...");

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "opencode",
                        Arguments = $"serve --port {port} --hostname {host}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    _serverProcess = Process.Start(startInfo);

                    if (_serverProcess == null)
                    {
                        OnServerStatusChanged?.Invoke("Failed to start OpenCode process");
                        return false;
                    }

                    // Don't block on output, just let it run
                    _serverProcess.EnableRaisingEvents = true;
                    _serverProcess.Exited += (s, e) =>
                    {
                        OnServerStatusChanged?.Invoke("OpenCode server stopped");
                        lock (_serverLock)
                        {
                            _serverProcess = null;
                        }
                    };
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    OnServerStatusChanged?.Invoke("OpenCode CLI not found. Install with: npm install -g opencode\nSee https://opencode.ai/docs/server/");
                    return false;
                }
                catch (Exception ex)
                {
                    OnServerStatusChanged?.Invoke($"Failed to start server: {ex.Message}");
                    return false;
                }
            }

            // Wait for server to become ready
            return await WaitForServerAsync();
        }

        /// <summary>
        /// Wait for the server to become healthy.
        /// </summary>
        private async Task<bool> WaitForServerAsync()
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(SERVER_START_TIMEOUT_MS);

            while (DateTime.Now - startTime < timeout)
            {
                if (await IsServerRunningAsync())
                {
                    OnServerStatusChanged?.Invoke("OpenCode server is ready");
                    return true;
                }

                await Task.Delay(500);
            }

            OnServerStatusChanged?.Invoke($"Server did not start within {SERVER_START_TIMEOUT_MS / 1000} seconds");
            return false;
        }

        /// <summary>
        /// Check if Node.js is installed on the system.
        /// </summary>
        private static bool IsNodeInstalled()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null) return false;
                    process.WaitForExit(5000);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensure the server is running, starting it if necessary.
        /// </summary>
        public async Task EnsureServerAsync()
        {
            if (await IsServerRunningAsync())
                return;

            if (autoStart)
            {
                var started = await StartServerAsync();
                if (!started)
                {
                    throw new Exception(
                        $"OpenCode server not running at {baseUrl} and failed to auto-start. " +
                        "Please run 'opencode serve' manually. See https://opencode.ai/docs/server/");
                }
            }
            else
            {
                throw new Exception(
                    $"OpenCode server not running at {baseUrl}. " +
                    "Start with: opencode serve");
            }
        }

        /// <summary>
        /// Stop the OpenCode server if we started it.
        /// </summary>
        public static void StopServer()
        {
            lock (_serverLock)
            {
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    OnServerStatusChanged?.Invoke("Stopping OpenCode server...");
                    try
                    {
                        _serverProcess.Kill();
                        _serverProcess.WaitForExit(5000);
                    }
                    catch
                    {
                        // Ignore errors when stopping
                    }
                    finally
                    {
                        _serverProcess = null;
                    }
                    OnServerStatusChanged?.Invoke("OpenCode server stopped");
                }
            }
        }

        /// <summary>
        /// Check if we started and are managing a server process.
        /// </summary>
        public static bool IsManagedServerRunning
        {
            get
            {
                lock (_serverLock)
                {
                    return _serverProcess != null && !_serverProcess.HasExited;
                }
            }
        }

        /// <summary>
        /// Start the AI Tool Server that handles callbacks from OpenCode's server-side tools.
        /// The server listens on port 19840 for tool execution requests.
        /// </summary>
        public static void StartToolServer()
        {
            lock (_toolServerLock)
            {
                if (_toolServer != null && _toolServer.IsRunning)
                {
                    Debug.WriteLine("[OpenCode] Tool server already running");
                    return;
                }

                _toolServer = new AIToolServer();
                _toolServer.CommandReceived += (sender, command) =>
                {
                    Debug.WriteLine($"[OpenCode] Tool command received: {command}");

                    // Collect command for UI display
                    lock (_commandsLock)
                    {
                        _collectedCommands.Add(command);
                    }

                    OnToolCommandReceived?.Invoke(sender, command);
                };
                _toolServer.Start();
                Debug.WriteLine($"[OpenCode] Tool server started on port {_toolServer.Port}");
            }
        }

        /// <summary>
        /// Stop the AI Tool Server.
        /// </summary>
        public static void StopToolServer()
        {
            lock (_toolServerLock)
            {
                if (_toolServer != null)
                {
                    _toolServer.Stop();
                    _toolServer.Dispose();
                    _toolServer = null;
                    Debug.WriteLine("[OpenCode] Tool server stopped");
                }
            }
        }

        /// <summary>
        /// Check if the AI Tool Server is running.
        /// </summary>
        public static bool IsToolServerRunning
        {
            get
            {
                lock (_toolServerLock)
                {
                    return _toolServer != null && _toolServer.IsRunning;
                }
            }
        }

        /// <summary>
        /// Test if the API connection is valid (auto-starts server if enabled)
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            // Try to ensure server is running (will auto-start if enabled)
            await EnsureServerAsync();

            // Try to create and delete a test session
            JObject session;
            try
            {
                session = await CreateSessionAsync("connection-test");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create test session: {ex.Message}");
            }

            if (session == null)
                throw new Exception("Failed to create test session - null response from server");

            if (!session.ContainsKey("id"))
                throw new Exception($"Failed to create test session - 'id' not in response. Response keys: {string.Join(", ", session.Properties().Select(p => p.Name))}");

            var sessionId = session["id"].ToString();
            try
            {
                await DeleteSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                // Ignore delete errors in test - session was created successfully
                System.Diagnostics.Debug.WriteLine($"Note: Delete session failed: {ex.Message}");
            }
            return true;
        }

        /// <summary>
        /// Process natural language instructions using function calling.
        /// Handles multi-turn conversations for query functions.
        /// Auto-starts the OpenCode server if not running.
        /// </summary>
        public async Task<string> ProcessInstructionsAsync(string mapContext, string userInstructions)
        {
            // Ensure OpenCode server is running (auto-start if needed)
            await EnsureServerAsync();

            // Ensure AI Tool Server is running (handles callbacks from OpenCode's server-side tools)
            StartToolServer();

            // Reset query tracker for this conversation
            _calledQueryFunctions.Clear();

            // Create a session for this conversation
            var session = await CreateSessionAsync("map-editor");
            if (session == null || !session.ContainsKey("id"))
            {
                throw new Exception("Failed to create OpenCode session");
            }

            var sessionId = session["id"].ToString();

            try
            {
                var systemPrompt = MapEditorPromptBuilder.LoadSystemPrompt();
                var userMessage = MapEditorPromptBuilder.BuildUserMessage(mapContext, userInstructions);
                var tools = AIToolConverter.ToSimpleFormat(MapEditorFunctions.GetToolDefinitions());

                Debug.WriteLine($"[OpenCode] User instructions: {userInstructions}");
                Debug.WriteLine($"[OpenCode] Tools count: {tools.Count}");

                var allCommands = new List<string>();
                int maxTurns = 40;

                for (int turn = 0; turn < maxTurns; turn++)
                {
                    Debug.WriteLine($"[OpenCode] Turn {turn + 1}/{maxTurns}");

                    var response = await SendMessageAsync(
                        sessionId,
                        userMessage,
                        tools,
                        systemPrompt,
                        turn == 0); // First turn requires tools

                    // Log raw response for debugging
                    Debug.WriteLine($"[OpenCode] Raw response: {response?.ToString(Formatting.None)?.Substring(0, Math.Min(2000, response?.ToString(Formatting.None)?.Length ?? 0))}");

                    var (commands, toolResponses, hasQueryFunctions) = ParseResponseWithQueries(response);

                    Debug.WriteLine($"[OpenCode] Parsed commands ({commands.Count}): {string.Join(" | ", commands)}");
                    Debug.WriteLine($"[OpenCode] Tool responses: {toolResponses.Count}, HasQueryFunctions: {hasQueryFunctions}");

                    // Add all action commands to the result
                    allCommands.AddRange(commands);

                    // If there were query function calls, continue the conversation
                    if (hasQueryFunctions && toolResponses.Count > 0)
                    {
                        // Send tool results back
                        foreach (var (toolCallId, result, isError) in toolResponses)
                        {
                            response = await SendToolResultAsync(sessionId, toolCallId, result, isError);
                        }

                        // Parse the follow-up response
                        var (followUpCommands, _, _) = ParseResponseWithQueries(response);
                        allCommands.AddRange(followUpCommands);

                        // Update user message to empty for continuation (context is in session)
                        userMessage = "";
                        continue;
                    }

                    // No query functions, we're done
                    break;
                }

                if (allCommands.Count == 0)
                {
                    Debug.WriteLine("[OpenCode] No commands generated");
                    return "# No commands generated";
                }

                var finalResult = string.Join(Environment.NewLine, allCommands);
                Debug.WriteLine($"[OpenCode] Final result ({allCommands.Count} commands):\n{finalResult}");
                return finalResult;
            }
            finally
            {
                // Clean up session
                try
                {
                    await DeleteSessionAsync(sessionId);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Create a new conversation session.
        /// </summary>
        private async Task<JObject> CreateSessionAsync(string title = null)
        {
            var body = new JObject();
            if (!string.IsNullOrEmpty(title))
            {
                body["title"] = title;
            }

            return await SendRequestAsync("POST", "/session", body);
        }

        /// <summary>
        /// Delete a session.
        /// </summary>
        private async Task DeleteSessionAsync(string sessionId)
        {
            await SendRequestAsync("DELETE", $"/session/{sessionId}");
        }

        /// <summary>
        /// Send a message to a session with tools.
        /// </summary>
        private async Task<JObject> SendMessageAsync(
            string sessionId,
            string text,
            JArray tools,
            string systemPrompt = null,
            bool requireTools = false)
        {
            var parts = new JArray
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = text
                }
            };

            var body = new JObject
            {
                ["parts"] = parts,
                ["maxOutputTokens"] = MAX_OUTPUT_TOKENS
            };

            // OpenCode's tools parameter enables server-side tools defined in .opencode/tool/ directory.
            // We pass boolean flags to enable our custom TypeScript tools.
            // The TypeScript tools call back to AIToolServer running in this application.
            if (tools != null && tools.Count > 0)
            {
                Debug.WriteLine($"[OpenCode] Enabling {tools.Count} server-side tools");

                // Convert to boolean format: {"tool_name": true, ...}
                var toolsObject = AIToolConverter.ToOpenCodeBooleanFormat(tools);
                Debug.WriteLine($"[OpenCode] Tools enabled: {toolsObject.ToString(Formatting.None)}");
                body["tools"] = toolsObject;
            }

            if (!string.IsNullOrEmpty(model))
            {
                // OpenCode expects model as object: { providerID: string, modelID: string }
                string providerId, modelId;
                if (model.Contains("/"))
                {
                    var parts2 = model.Split(new[] { '/' }, 2);
                    providerId = parts2[0];
                    modelId = parts2[1];
                }
                else
                {
                    providerId = "anthropic";
                    modelId = model;
                }

                body["model"] = new JObject
                {
                    ["providerID"] = providerId,
                    ["modelID"] = modelId
                };
            }

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                body["system"] = systemPrompt;
            }

            // Log the full request (truncated for readability)
            var bodyStr = body.ToString(Formatting.None);
            Debug.WriteLine($"[OpenCode] Request body (first 3000 chars): {bodyStr.Substring(0, Math.Min(3000, bodyStr.Length))}");

            return await SendRequestAsync("POST", $"/session/{sessionId}/message", body);
        }

        /// <summary>
        /// Send a tool execution result back to continue the conversation.
        /// </summary>
        private async Task<JObject> SendToolResultAsync(
            string sessionId,
            string toolCallId,
            string result,
            bool isError = false)
        {
            var parts = new JArray
            {
                new JObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolCallId,
                    ["content"] = result,
                    ["is_error"] = isError
                }
            };

            var body = new JObject
            {
                ["parts"] = parts
            };

            return await SendRequestAsync("POST", $"/session/{sessionId}/message", body);
        }

        /// <summary>
        /// Parse the response, separating query functions from action functions.
        /// Returns: (action commands, tool responses for multi-turn, has query functions)
        /// </summary>
        private (List<string> commands, List<(string toolCallId, string result, bool isError)> toolResponses, bool hasQueryFunctions)
            ParseResponseWithQueries(JObject response)
        {
            var commands = new List<string>();
            var toolResponses = new List<(string toolCallId, string result, bool isError)>();
            bool hasQueryFunctions = false;

            if (response == null)
                return (new List<string> { "# No response from AI" }, toolResponses, false);

            // OpenCode returns: { "info": {...}, "parts": [...] }
            var parts = response["parts"] as JArray;
            if (parts == null || parts.Count == 0)
            {
                // Check for error
                var error = response["error"]?.ToString();
                if (!string.IsNullOrEmpty(error))
                {
                    return (new List<string> { $"# Error: {error}" }, toolResponses, false);
                }

                // Check for direct text content (fallback)
                var text = ExtractTextContent(response);
                if (!string.IsNullOrEmpty(text))
                {
                    commands.Add("# AI Response:");
                    commands.Add(text);
                }
                return (commands, toolResponses, false);
            }

            Debug.WriteLine($"[OpenCode] Parsing {parts.Count} parts");

            foreach (var part in parts)
            {
                var partType = part["type"]?.ToString();
                Debug.WriteLine($"[OpenCode] Part type: {partType}");

                if (partType == "text")
                {
                    // Text content - might contain reasoning
                    var text = part["text"]?.ToString();
                    Debug.WriteLine($"[OpenCode] Text part: {text?.Substring(0, Math.Min(200, text?.Length ?? 0))}");
                    if (!string.IsNullOrEmpty(text) && !text.StartsWith("#"))
                    {
                        // Only add non-empty text as comments
                        if (text.Length > 10)
                        {
                            commands.Add($"# {text.Substring(0, Math.Min(100, text.Length))}...");
                        }
                    }
                }
                else if (partType == "tool_use" || partType == "tool-invocation")
                {
                    // Tool call from the AI
                    var toolCallId = part["id"]?.ToString() ?? Guid.NewGuid().ToString();
                    var functionName = part["name"]?.ToString();
                    var arguments = part["input"] as JObject ?? part["args"] as JObject ?? part["arguments"] as JObject;

                    Debug.WriteLine($"[OpenCode] Tool call: {functionName}, args: {arguments?.ToString(Formatting.None)}");

                    if (string.IsNullOrEmpty(functionName))
                    {
                        Debug.WriteLine($"[OpenCode] WARNING: Tool call with no function name, part: {part?.ToString(Formatting.None)}");
                        continue;
                    }

                    if (arguments == null)
                    {
                        Debug.WriteLine($"[OpenCode] WARNING: Tool call {functionName} has no arguments, part: {part?.ToString(Formatting.None)}");
                        // Try to create empty arguments
                        arguments = new JObject();
                    }

                    try
                    {
                        // Check if this is a query function
                        if (MapEditorFunctions.IsQueryFunction(functionName))
                        {
                            hasQueryFunctions = true;
                            _calledQueryFunctions.Add(functionName);
                            commands.Add($"# QUERY: {functionName} called");
                            var result = MapEditorFunctions.ExecuteQueryFunction(functionName, arguments);
                            Debug.WriteLine($"[OpenCode] Query {functionName} result: {result?.Substring(0, Math.Min(500, result?.Length ?? 0))}");
                            toolResponses.Add((toolCallId, result, false));
                        }
                        else
                        {
                            // Check if this action function requires a query
                            var requiredQuery = MapEditorFunctions.GetRequiredQuery(functionName);
                            if (requiredQuery != null && !_calledQueryFunctions.Contains(requiredQuery))
                            {
                                commands.Add($"# WARNING: {functionName} called without {requiredQuery}");
                                var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);
                                Debug.WriteLine($"[OpenCode] Generated command (with warning): {command}");
                                commands.Add(command);
                                toolResponses.Add((toolCallId, $"Command queued: {functionName} (WARNING: {requiredQuery} was not called first)", false));
                            }
                            else
                            {
                                var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);
                                Debug.WriteLine($"[OpenCode] Generated command: {command}");
                                commands.Add(command);
                                toolResponses.Add((toolCallId, $"Command queued: {functionName}", false));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OpenCode] ERROR parsing {functionName}: {ex.Message}\n{ex.StackTrace}");
                        commands.Add($"# Error parsing {functionName}: {ex.Message}");
                        toolResponses.Add((toolCallId, $"Error: {ex.Message}", true));
                    }
                }
            }

            return (commands, toolResponses, hasQueryFunctions);
        }

        /// <summary>
        /// Extract text content from various response formats.
        /// </summary>
        private string ExtractTextContent(JObject response)
        {
            // Check parts array
            var parts = response["parts"] as JArray;
            if (parts != null)
            {
                var texts = new List<string>();
                foreach (var part in parts)
                {
                    if (part["type"]?.ToString() == "text")
                    {
                        var text = part["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                            texts.Add(text);
                    }
                }
                if (texts.Count > 0)
                    return string.Join("\n", texts);
            }

            // Fallback checks
            if (response["content"] != null)
            {
                var content = response["content"];
                if (content.Type == JTokenType.String)
                    return content.ToString();
                if (content.Type == JTokenType.Array)
                {
                    var texts = new List<string>();
                    foreach (var block in content)
                    {
                        if (block["type"]?.ToString() == "text")
                            texts.Add(block["text"]?.ToString() ?? "");
                    }
                    return string.Join("\n", texts);
                }
            }

            if (response["text"] != null)
                return response["text"].ToString();

            if (response["message"] != null)
                return ExtractTextContent(response["message"] as JObject);

            return null;
        }

        /// <summary>
        /// Send a simple text prompt without function calling.
        /// Useful for planning/orchestration where structured output isn't needed.
        /// Auto-starts the OpenCode server if not running.
        /// </summary>
        /// <param name="systemPrompt">System prompt</param>
        /// <param name="userPrompt">User prompt</param>
        /// <returns>Text response from the model</returns>
        public async Task<string> SendSimplePromptAsync(string systemPrompt, string userPrompt)
        {
            // Ensure server is running (auto-start if needed)
            await EnsureServerAsync();

            // Create a session for this request
            var session = await CreateSessionAsync("simple-prompt");
            if (session == null || !session.ContainsKey("id"))
            {
                throw new Exception("Failed to create OpenCode session");
            }

            var sessionId = session["id"].ToString();

            try
            {
                var parts = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = userPrompt
                    }
                };

                var body = new JObject
                {
                    ["parts"] = parts,
                    ["maxOutputTokens"] = MAX_OUTPUT_TOKENS
                };

                if (!string.IsNullOrEmpty(model))
                {
                    string providerId, modelId;
                    if (model.Contains("/"))
                    {
                        var modelParts = model.Split(new[] { '/' }, 2);
                        providerId = modelParts[0];
                        modelId = modelParts[1];
                    }
                    else
                    {
                        providerId = "anthropic";
                        modelId = model;
                    }

                    body["model"] = new JObject
                    {
                        ["providerID"] = providerId,
                        ["modelID"] = modelId
                    };
                }

                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    body["system"] = systemPrompt;
                }

                var response = await SendRequestAsync("POST", $"/session/{sessionId}/message", body);
                return ExtractTextContent(response) ?? "";
            }
            finally
            {
                try
                {
                    await DeleteSessionAsync(sessionId);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Make HTTP request to OpenCode server.
        /// </summary>
        private async Task<JObject> SendRequestAsync(string method, string endpoint, JObject body = null)
        {
            var url = $"{baseUrl}{endpoint}";
            string responseContent = null;

            try
            {
                HttpResponseMessage response;

                using (var request = new HttpRequestMessage(new HttpMethod(method), url))
                {
                    if (body != null)
                    {
                        request.Content = new StringContent(
                            body.ToString(Formatting.None),
                            Encoding.UTF8,
                            "application/json");
                    }

                    using (var cts = new System.Threading.CancellationTokenSource(REQUEST_TIMEOUT_MS))
                    {
                        response = await httpClient.SendAsync(request, cts.Token);
                    }
                }

                responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[OpenCode] API Error Response ({response.StatusCode}): {responseContent}");
                    var errorDetail = "";
                    try
                    {
                        var errorJson = JObject.Parse(responseContent);
                        errorDetail = errorJson["error"]?.ToString() ?? errorJson["message"]?.ToString() ?? responseContent;
                    }
                    catch
                    {
                        errorDetail = responseContent;
                    }
                    throw new Exception($"OpenCode API error: {response.StatusCode} - {errorDetail}");
                }

                // Handle empty responses
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    return new JObject();
                }

                var trimmed = responseContent.Trim();

                // Handle array responses (some endpoints return arrays)
                if (trimmed.StartsWith("["))
                {
                    // Wrap array in an object for consistent handling
                    return new JObject { ["items"] = JArray.Parse(responseContent) };
                }

                // Handle primitive responses (boolean, number, string)
                // DELETE endpoints often return just "true" or "false"
                if (trimmed == "true" || trimmed == "false")
                {
                    return new JObject { ["success"] = trimmed == "true" };
                }

                // Handle numeric responses
                if (double.TryParse(trimmed, out var numValue))
                {
                    return new JObject { ["value"] = numValue };
                }

                // Handle quoted string responses
                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                {
                    return new JObject { ["value"] = trimmed.Substring(1, trimmed.Length - 2) };
                }

                // Handle null response
                if (trimmed == "null")
                {
                    return new JObject();
                }

                return JObject.Parse(responseContent);
            }
            catch (TaskCanceledException)
            {
                throw new Exception($"OpenCode request timed out after {REQUEST_TIMEOUT_MS / 1000} seconds");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"OpenCode connection failed: {ex.Message}. Is the server running at {baseUrl}?");
            }
            catch (JsonReaderException ex)
            {
                // Log and include the actual response for debugging
                Debug.WriteLine($"[OpenCode] JSON parse error: {ex.Message}");
                Debug.WriteLine($"[OpenCode] Response content: {responseContent}");
                throw new Exception($"OpenCode returned invalid JSON: {ex.Message}. Response was: {responseContent?.Substring(0, Math.Min(500, responseContent?.Length ?? 0))}");
            }
        }

        #region High-Level Tool Execution API

        /// <summary>
        /// Extract tool calls from an OpenCode response.
        /// Handles both tool_use and tool-invocation part types.
        /// </summary>
        /// <param name="response">The raw response from OpenCode</param>
        /// <returns>List of extracted tool calls</returns>
        public List<OpenCodeToolCall> ExtractToolCalls(JObject response)
        {
            var toolCalls = new List<OpenCodeToolCall>();

            var parts = response?["parts"] as JArray;
            if (parts == null) return toolCalls;

            foreach (var part in parts)
            {
                var partType = part["type"]?.ToString();
                if (partType == "tool_use" || partType == "tool-invocation")
                {
                    var rawArgs = part["input"] as JObject ?? part["args"] as JObject ?? part["arguments"] as JObject ?? new JObject();

                    var toolCall = new OpenCodeToolCall
                    {
                        Id = part["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                        Name = part["name"]?.ToString() ?? "unknown",
                        RawArguments = rawArgs,
                        Arguments = new Dictionary<string, object>()
                    };

                    // Convert JObject to Dictionary
                    foreach (var prop in rawArgs.Properties())
                    {
                        toolCall.Arguments[prop.Name] = prop.Value.ToObject<object>();
                    }

                    toolCalls.Add(toolCall);
                }
            }

            return toolCalls;
        }

        /// <summary>
        /// Send a prompt with optional images and tools.
        /// This is a lower-level method for more control over the request.
        /// </summary>
        /// <param name="sessionId">Session ID to use</param>
        /// <param name="text">Text prompt</param>
        /// <param name="tools">Optional tools to enable (in simple or OpenAI format)</param>
        /// <param name="images">Optional list of image file paths to include</param>
        /// <param name="systemPrompt">Optional system prompt</param>
        /// <param name="thinkingBudget">Optional thinking budget for extended thinking (Claude)</param>
        /// <returns>Raw response from OpenCode</returns>
        public async Task<JObject> SendPromptWithImagesAsync(
            string sessionId,
            string text,
            JArray tools = null,
            List<string> images = null,
            string systemPrompt = null,
            int? thinkingBudget = null)
        {
            var parts = new JArray();

            // Add images first (if any)
            if (images != null && images.Count > 0)
            {
                foreach (var imagePath in images)
                {
                    var absPath = System.IO.Path.GetFullPath(imagePath);
                    var mimeType = GetMimeType(imagePath);
                    var encodedPath = System.Web.HttpUtility.UrlEncode(absPath);

                    parts.Add(new JObject
                    {
                        ["type"] = "file",
                        ["mime"] = mimeType,
                        ["filename"] = System.IO.Path.GetFileName(imagePath),
                        ["url"] = $"file://{encodedPath}"
                    });
                }
            }

            // Add text prompt
            parts.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = text
            });

            var body = new JObject
            {
                ["parts"] = parts,
                ["maxOutputTokens"] = MAX_OUTPUT_TOKENS
            };

            // Add thinking budget if specified
            if (thinkingBudget.HasValue && thinkingBudget.Value > 0)
            {
                body["thinking"] = new JObject
                {
                    ["type"] = "enabled",
                    ["budgetTokens"] = thinkingBudget.Value
                };
            }

            // Add tools if specified
            // OpenCode supports two modes:
            // 1. Server-side tools: {"tool_name": true} - enables TypeScript tools in .opencode/tool/
            // 2. Client-side tools: [{"name": "...", "description": "...", "parameters": {...}}] - full definitions
            // We use client-side mode here for function calling
            if (tools != null && tools.Count > 0)
            {
                var simpleTools = AIToolConverter.ToSimpleFormat(tools);
                body["tools"] = simpleTools;
            }

            // Add model specification
            if (!string.IsNullOrEmpty(model))
            {
                var modelSpec = OpenCodeModelSpec.Parse(model);
                if (modelSpec != null)
                {
                    body["model"] = modelSpec.ToJson();
                }
            }

            // Add system prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                body["system"] = systemPrompt;
            }

            return await SendRequestAsync("POST", $"/session/{sessionId}/message", body);
        }

        /// <summary>
        /// High-level method to run a prompt with tools, automatically handling the tool execution loop.
        /// This is the recommended API for most use cases with function calling.
        /// </summary>
        /// <param name="text">User prompt text</param>
        /// <param name="tools">Tools to make available (in OpenAI or simple format)</param>
        /// <param name="toolExecutor">Function to execute tool calls: (toolName, arguments) => result</param>
        /// <param name="images">Optional list of image file paths to include</param>
        /// <param name="systemPrompt">Optional system prompt</param>
        /// <param name="sessionTitle">Title for the session (for debugging)</param>
        /// <param name="maxIterations">Maximum tool call iterations</param>
        /// <param name="thinkingBudget">Optional thinking budget for Claude's extended thinking</param>
        /// <returns>Result containing final text and all tool calls made</returns>
        public async Task<RunWithToolsResult> RunWithToolsAsync(
            string text,
            JArray tools,
            Func<string, JObject, Task<object>> toolExecutor,
            List<string> images = null,
            string systemPrompt = null,
            string sessionTitle = "tool-session",
            int maxIterations = 10,
            int? thinkingBudget = null)
        {
            // Ensure server is running
            await EnsureServerAsync();

            var result = new RunWithToolsResult
            {
                ToolCallsMade = new List<OpenCodeToolCallResult>()
            };

            // Create session
            var session = await CreateSessionAsync(sessionTitle);
            if (session == null || !session.ContainsKey("id"))
            {
                result.Success = false;
                result.Error = "Failed to create OpenCode session";
                return result;
            }

            var sessionId = session["id"].ToString();

            try
            {
                // Send initial prompt with tools
                var response = await SendPromptWithImagesAsync(
                    sessionId, text, tools, images, systemPrompt, thinkingBudget);

                Debug.WriteLine($"[OpenCode] RunWithTools initial response received");

                // Tool execution loop
                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    var toolCalls = ExtractToolCalls(response);

                    if (toolCalls.Count == 0)
                    {
                        // No more tool calls, we're done
                        Debug.WriteLine($"[OpenCode] RunWithTools completed after {iteration} iterations");
                        break;
                    }

                    Debug.WriteLine($"[OpenCode] RunWithTools iteration {iteration + 1}: {toolCalls.Count} tool calls");

                    // Execute each tool call and send results
                    foreach (var toolCall in toolCalls)
                    {
                        Debug.WriteLine($"[OpenCode] Executing tool: {toolCall.Name}");

                        object toolResult;
                        bool isError = false;

                        try
                        {
                            toolResult = await toolExecutor(toolCall.Name, toolCall.RawArguments);
                        }
                        catch (Exception ex)
                        {
                            toolResult = new { error = ex.Message };
                            isError = true;
                            Debug.WriteLine($"[OpenCode] Tool {toolCall.Name} failed: {ex.Message}");
                        }

                        // Track the tool call
                        result.ToolCallsMade.Add(new OpenCodeToolCallResult
                        {
                            Name = toolCall.Name,
                            Arguments = toolCall.Arguments,
                            Result = toolResult,
                            IsError = isError
                        });

                        // Send tool result back
                        response = await SendToolResultAsync(sessionId, toolCall.Id,
                            toolResult is string s ? s : JsonConvert.SerializeObject(toolResult),
                            isError);
                    }

                    result.Iterations = iteration + 1;
                }

                // Extract final text response
                result.Text = ExtractTextContent(response) ?? "";
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                Debug.WriteLine($"[OpenCode] RunWithTools error: {ex.Message}");
            }
            finally
            {
                // Clean up session
                try
                {
                    await DeleteSessionAsync(sessionId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenCode] Failed to delete session: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Synchronous wrapper for tool executor that accepts sync functions.
        /// Use this overload when your tool executor is synchronous.
        /// </summary>
        public async Task<RunWithToolsResult> RunWithToolsAsync(
            string text,
            JArray tools,
            Func<string, JObject, object> toolExecutor,
            List<string> images = null,
            string systemPrompt = null,
            string sessionTitle = "tool-session",
            int maxIterations = 10,
            int? thinkingBudget = null)
        {
            // Wrap sync executor in async
            return await RunWithToolsAsync(
                text,
                tools,
                (name, args) => Task.FromResult(toolExecutor(name, args)),
                images,
                systemPrompt,
                sessionTitle,
                maxIterations,
                thinkingBudget);
        }

        /// <summary>
        /// Get MIME type for a file based on extension.
        /// </summary>
        private static string GetMimeType(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".bmp": return "image/bmp";
                case ".pdf": return "application/pdf";
                case ".txt": return "text/plain";
                case ".csv": return "text/csv";
                case ".json": return "application/json";
                case ".xml": return "application/xml";
                default: return "application/octet-stream";
            }
        }

        #endregion
    }

    #region OpenCode Session Helper

    /// <summary>
    /// Helper class for managing OpenCode sessions with automatic cleanup.
    /// Implements IDisposable for use with 'using' statements.
    /// </summary>
    /// <example>
    /// using (var session = await OpenCodeSession.CreateAsync(client, "my-session"))
    /// {
    ///     var response = await session.PromptAsync("Hello!");
    ///     // Session is automatically deleted when disposed
    /// }
    /// </example>
    public class OpenCodeSession : IDisposable
    {
        private readonly OpenCodeClient _client;
        private readonly string _sessionId;
        private bool _disposed = false;

        /// <summary>
        /// The session ID
        /// </summary>
        public string SessionId => _sessionId;

        private OpenCodeSession(OpenCodeClient client, string sessionId)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        }

        /// <summary>
        /// Create a new session asynchronously.
        /// </summary>
        /// <param name="client">OpenCode client to use</param>
        /// <param name="title">Optional session title</param>
        /// <returns>A new OpenCodeSession instance</returns>
        public static async Task<OpenCodeSession> CreateAsync(OpenCodeClient client, string title = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            await client.EnsureServerAsync();

            // Use reflection to access the private CreateSessionAsync method
            // This is a workaround since CreateSessionAsync is private
            var createMethod = typeof(OpenCodeClient).GetMethod("CreateSessionAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (createMethod == null)
            {
                throw new InvalidOperationException("Cannot find CreateSessionAsync method");
            }

            var task = (Task<JObject>)createMethod.Invoke(client, new object[] { title });
            var session = await task;

            if (session == null || !session.ContainsKey("id"))
            {
                throw new Exception("Failed to create OpenCode session");
            }

            return new OpenCodeSession(client, session["id"].ToString());
        }

        /// <summary>
        /// Send a prompt to this session.
        /// </summary>
        public async Task<JObject> PromptAsync(
            string text,
            JArray tools = null,
            List<string> images = null,
            string systemPrompt = null,
            int? thinkingBudget = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpenCodeSession));

            return await _client.SendPromptWithImagesAsync(
                _sessionId, text, tools, images, systemPrompt, thinkingBudget);
        }

        /// <summary>
        /// Dispose the session, deleting it from the server.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Use reflection to access the private DeleteSessionAsync method
                var deleteMethod = typeof(OpenCodeClient).GetMethod("DeleteSessionAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (deleteMethod != null)
                {
                    var task = (Task)deleteMethod.Invoke(_client, new object[] { _sessionId });
                    task.Wait(5000); // Wait up to 5 seconds for cleanup
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion
}
