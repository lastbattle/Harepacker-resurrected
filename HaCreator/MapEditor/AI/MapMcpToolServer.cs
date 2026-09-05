using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// A small loopback MCP host for the active HaCreator map tools.
    /// The same registry is also used by the OpenAI-compatible client, so external
    /// MCP clients and model function calling see one tool contract.
    /// </summary>
    public sealed class MapMcpToolServer : IDisposable
    {
        private const string ProtocolVersion = "2025-03-26";
        private const string LocalHost = "127.0.0.1";
        private const int FirstPort = 19841;

        private readonly HttpListener listener = new HttpListener();
        private readonly object lifecycleLock = new object();
        private readonly object queryLock = new object();
        private readonly HashSet<string> calledQueries = new HashSet<string>(StringComparer.Ordinal);
        private CancellationTokenSource cancellation;
        private Task listenerTask;
        private bool disposed;

        public MapMcpToolServer(int port = 0)
        {
            Port = port > 0 ? port : FindAvailablePort();
            Endpoint = $"http://{LocalHost}:{Port}/mcp";
            AuthorizationToken = Guid.NewGuid().ToString("N");
            listener.Prefixes.Add($"http://{LocalHost}:{Port}/");
        }

        public int Port { get; }
        public string Endpoint { get; }
        public string AuthorizationToken { get; }
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Raised when an action tool produces a command for the map editor.
        /// A configured CommandExecutor is invoked before this event.
        /// </summary>
        public event EventHandler<string> CommandReceived;

        /// <summary>
        /// Optional board-aware query callback supplied by the editor window.
        /// </summary>
        public Func<string, JObject, string> QueryExecutor { get; set; }

        /// <summary>Optional query callback returning standard MCP text and image blocks. Return null to use the text callback.</summary>
        public Func<string, JObject, JArray> RichQueryExecutor { get; set; }

        /// <summary>
        /// Optional board-aware action callback supplied by the editor window.
        /// </summary>
        public Func<string, JObject, string> ActionExecutor { get; set; }

        /// <summary>
        /// Optional callback that applies a generated command to the active board.
        /// When supplied, MCP action calls are autonomous and report application
        /// failures back to the caller.
        /// </summary>
        public Func<string, string> CommandExecutor { get; set; }

        public void Start()
        {
            lock (lifecycleLock)
            {
                ThrowIfDisposed();
                if (IsRunning)
                    return;

                listener.Start();
                cancellation = new CancellationTokenSource();
                IsRunning = true;
                listenerTask = Task.Run(() => ListenLoopAsync(cancellation.Token));
            }
        }

        public void Stop()
        {
            lock (lifecycleLock)
            {
                if (!IsRunning)
                    return;

                cancellation?.Cancel();
                listener.Stop();
                IsRunning = false;
            }
        }

        public void ResetConversationState()
        {
            lock (queryLock)
            {
                calledQueries.Clear();
            }
        }

        /// <summary>
        /// Return the MCP tools/list representation.
        /// </summary>
        public JArray GetMcpTools()
        {
            var result = new JArray();
            foreach (var tool in MapEditorFunctions.GetToolDefinitions().OfType<JObject>())
            {
                var function = tool["function"] as JObject;
                if (function == null)
                    continue;

                result.Add(new JObject
                {
                    ["name"] = function["name"],
                    ["description"] = function["description"],
                    ["inputSchema"] = function["parameters"]?.DeepClone() ?? new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                    }
                });
            }
            return result;
        }

        /// <summary>
        /// Return the standard Chat Completions function-tool representation.
        /// </summary>
        public JArray GetChatCompletionTools()
        {
            return GetChatCompletionTools(strict: false);
        }

        /// <summary>
        /// Return Chat Completions tools, optionally normalized for strict schemas.
        /// </summary>
        public JArray GetChatCompletionTools(bool strict)
        {
            var result = (JArray)MapEditorFunctions.GetToolDefinitions().DeepClone();
            if (!strict)
                return result;

            foreach (var tool in result.OfType<JObject>())
            {
                var function = tool["function"] as JObject;
                if (function == null)
                    continue;

                function["parameters"] = CreateStrictSchema(function["parameters"] as JObject);
                function["strict"] = true;
            }

            return result;
        }

        /// <summary>
        /// Return the standard Responses API function-tool representation.
        /// </summary>
        public JArray GetResponsesTools(bool strict)
        {
            var result = new JArray();
            foreach (var tool in GetMcpTools().OfType<JObject>())
            {
                result.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = tool["name"],
                    ["description"] = tool["description"],
                    ["parameters"] = strict
                        ? CreateStrictSchema(tool["inputSchema"] as JObject)
                        : tool["inputSchema"],
                    ["strict"] = strict
                });
            }
            return result;
        }

        private static JObject CreateStrictSchema(JObject schema)
        {
            var result = schema != null
                ? (JObject)schema.DeepClone()
                : new JObject { ["type"] = "object", ["properties"] = new JObject() };

            if (string.Equals(result["type"]?.ToString(), "object", StringComparison.OrdinalIgnoreCase))
            {
                var properties = result["properties"] as JObject;
                if (properties != null)
                {
                    var required = new HashSet<string>((result["required"] as JArray ?? new JArray()).Values<string>());
                    result["additionalProperties"] = false;
                    result["required"] = new JArray(properties.Properties().Select(property => property.Name));

                    foreach (var property in properties.Properties().ToList())
                    {
                        if (property.Value is JObject propertySchema)
                        {
                            var normalized = CreateStrictSchema(propertySchema);
                            if (!required.Contains(property.Name))
                                normalized = new JObject { ["anyOf"] = new JArray(normalized, new JObject { ["type"] = "null" }) };
                            property.Value = normalized;
                        }
                    }
                }
            }

            if (result["items"] is JObject items)
                result["items"] = CreateStrictSchema(items);

            if (result["anyOf"] is JArray anyOf)
            {
                for (var index = 0; index < anyOf.Count; index++)
                {
                    if (anyOf[index] is JObject option)
                        anyOf[index] = CreateStrictSchema(option);
                }
            }

            return result;
        }

        /// <summary>
        /// Invoke a tool using the same semantics as the MCP tools/call method.
        /// </summary>
        public MapMcpToolCallResult CallTool(string toolName, JObject arguments, bool enforceQueryOrder = true)
        {
            arguments ??= new JObject();
            // Strict API schemas represent omitted optional fields as null.
            // Preserve the existing command builders' absent-property semantics.
            arguments = (JObject)arguments.DeepClone();
            foreach (var property in arguments.Properties().Where(p => p.Value.Type == JTokenType.Null).ToList())
                property.Remove();

            var definition = GetMcpTools().OfType<JObject>().FirstOrDefault(t => string.Equals(
                t["name"]?.ToString(), toolName, StringComparison.Ordinal));
            if (definition == null)
            {
                return MapMcpToolCallResult.Error(toolName, $"Unknown map tool: {toolName}");
            }

            var requiredArguments = definition["inputSchema"]?["required"] as JArray;
            var missing = requiredArguments?.Values<string>().Where(name => arguments[name] == null).ToArray();
            if (missing?.Length > 0)
                return MapMcpToolCallResult.Error(toolName, $"Error: Missing required arguments: {string.Join(", ", missing)}.");
            var properties = definition["inputSchema"]?["properties"] as JObject;
            foreach (var argument in arguments.Properties())
            {
                if (properties?[argument.Name] is not JObject schema)
                    return MapMcpToolCallResult.Error(toolName, $"Error: Unknown argument '{argument.Name}'.");
                var type = schema["type"]?.ToString();
                var validType = type switch
                {
                    "integer" => argument.Value.Type == JTokenType.Integer,
                    "number" => argument.Value.Type == JTokenType.Integer || argument.Value.Type == JTokenType.Float,
                    "boolean" => argument.Value.Type == JTokenType.Boolean,
                    "string" => argument.Value.Type == JTokenType.String,
                    "array" => argument.Value.Type == JTokenType.Array,
                    "object" => argument.Value.Type == JTokenType.Object,
                    _ => true
                };
                if (!validType)
                    return MapMcpToolCallResult.Error(toolName, $"Error: '{argument.Name}' must be {type}.");
                if (argument.Value.Type == JTokenType.String && !MapEditorFunctions.IsQueryFunction(toolName) &&
                    argument.Value.Value<string>().IndexOfAny(new[] { '\r', '\n', '"' }) >= 0)
                    return MapMcpToolCallResult.Error(toolName, $"Error: '{argument.Name}' cannot contain line breaks or double quotes in a map command.");
                if (schema["enum"] is JArray allowed && !allowed.Any(value => JToken.DeepEquals(value, argument.Value)))
                    return MapMcpToolCallResult.Error(toolName, $"Error: Invalid value for '{argument.Name}'. Allowed: {string.Join(", ", allowed)}.");
                if (argument.Value.Type == JTokenType.Integer || argument.Value.Type == JTokenType.Float)
                {
                    var value = argument.Value.Value<double>();
                    if (schema["minimum"] != null && value < schema["minimum"].Value<double>() ||
                        schema["maximum"] != null && value > schema["maximum"].Value<double>())
                        return MapMcpToolCallResult.Error(toolName, $"Error: '{argument.Name}' is outside the allowed range.");
                }
            }

            var selectorError = MapEditorFunctions.ValidateActionSelector(toolName, arguments);
            if (selectorError != null)
                return MapMcpToolCallResult.Error(toolName, "Error: " + selectorError);

            try
            {
                if (MapEditorFunctions.IsQueryFunction(toolName))
                {
                    var content = RichQueryExecutor?.Invoke(toolName, arguments);
                    var result = content != null
                        ? MapMcpToolCallResult.Query(toolName, content)
                        : MapMcpToolCallResult.Query(toolName, QueryExecutor != null
                            ? QueryExecutor(toolName, arguments)
                            : MapEditorFunctions.ExecuteQueryFunction(toolName, arguments));
                    if (result.Success)
                    {
                        lock (queryLock)
                            calledQueries.Add(toolName);
                    }
                    return result;
                }

                var requiredQuery = MapEditorFunctions.GetRequiredQuery(toolName);
                if (enforceQueryOrder && requiredQuery != null)
                {
                    lock (queryLock)
                    {
                        if (!calledQueries.Contains(requiredQuery))
                        {
                            return MapMcpToolCallResult.Error(
                                toolName,
                                MapEditorFunctions.GetQueryRequiredError(toolName, requiredQuery));
                        }
                    }
                }

                var command = ActionExecutor != null
                    ? ActionExecutor(toolName, arguments)
                    : MapEditorFunctions.FunctionCallToCommand(toolName, arguments);

                if (string.IsNullOrWhiteSpace(command) || command.StartsWith("#", StringComparison.Ordinal))
                    return MapMcpToolCallResult.Error(toolName, command ?? "The tool did not produce a command.");

                string executionResult = null;
                if (CommandExecutor != null)
                {
                    executionResult = CommandExecutor(command);
                    if (!string.IsNullOrWhiteSpace(executionResult) &&
                        executionResult.StartsWith("# ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        return MapMcpToolCallResult.Error(toolName, executionResult);
                    }
                }

                CommandReceived?.Invoke(this, command);
                return MapMcpToolCallResult.Action(toolName, command, executionResult);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapMcpToolServer] {toolName} failed: {ex}");
                return MapMcpToolCallResult.Error(toolName, ex.Message);
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MapMcpToolServer] Listen error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var response = context.Response;
            try
            {
                if (!IsAuthorized(context.Request))
                {
                    await SendJsonAsync(response, null, 401, new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new JObject { ["code"] = -32001, ["message"] = "Unauthorized" }
                    }).ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    await SendJsonAsync(response, null, 405, new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new JObject { ["code"] = -32600, ["message"] = "POST is required" }
                    }).ConfigureAwait(false);
                    return;
                }

                string body;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var request = JObject.Parse(body);
                var id = request["id"]?.DeepClone();
                var method = request["method"]?.ToString();

                if (string.Equals(method, "notifications/initialized", StringComparison.Ordinal))
                {
                    response.StatusCode = 202;
                    response.Close();
                    return;
                }

                var result = HandleRpcRequest(method, request["params"] as JObject ?? new JObject());
                await SendJsonAsync(response, id, 200, result).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                await SendJsonAsync(response, null, 400, new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["error"] = new JObject { ["code"] = -32700, ["message"] = ex.Message }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapMcpToolServer] Request error: {ex}");
                try
                {
                    await SendJsonAsync(response, null, 500, new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new JObject { ["code"] = -32603, ["message"] = ex.Message }
                    }).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private JObject HandleRpcRequest(string method, JObject parameters)
        {
            switch (method)
            {
                case "initialize":
                    return new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["result"] = new JObject
                        {
                            ["protocolVersion"] = ProtocolVersion,
                            ["capabilities"] = new JObject { ["tools"] = new JObject() },
                            ["serverInfo"] = new JObject { ["name"] = "harepacker-map", ["version"] = "1.0.0" },
                            ["instructions"] = "Map tools operate on the active HaCreator map and return staged commands."
                        }
                    };

                case "ping":
                    return new JObject { ["jsonrpc"] = "2.0", ["result"] = new JObject() };

                case "tools/list":
                    return new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["result"] = new JObject { ["tools"] = GetMcpTools() }
                    };

                case "tools/call":
                    var name = parameters["name"]?.ToString();
                    var args = parameters["arguments"] as JObject ?? new JObject();
                    var call = CallTool(name, args, enforceQueryOrder: true);
                    return new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["result"] = new JObject
                        {
                            ["isError"] = !call.Success,
                            ["content"] = call.Content
                        }
                    };

                default:
                    return new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new JObject { ["code"] = -32601, ["message"] = $"Unknown method: {method}" }
                    };
            }
        }

        private bool IsAuthorized(HttpListenerRequest request)
        {
            var authorization = request.Headers["Authorization"];
            return string.Equals(authorization, $"Bearer {AuthorizationToken}", StringComparison.Ordinal);
        }

        private static async Task SendJsonAsync(HttpListenerResponse response, JToken id, int statusCode, JObject payload)
        {
            if (id != null)
                payload["id"] = id;

            var bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            response.Close();
        }

        private static int FindAvailablePort()
        {
            for (var port = FirstPort; port < FirstPort + 100; port++)
            {
                try
                {
                    using (var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port))
                    {
                        probe.Start();
                        probe.Stop();
                    }
                    return port;
                }
                catch (SocketException) { }
            }

            throw new InvalidOperationException("No local MCP port is available.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MapMcpToolServer));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
            cancellation?.Dispose();
            listener.Close();
        }
    }

    public sealed class MapMcpToolCallResult
    {
        public string ToolName { get; private set; }
        public bool Success { get; private set; }
        public bool IsQuery { get; private set; }
        public string Text { get; private set; }
        public string Command { get; private set; }
        public JArray Content { get; private set; }

        public static MapMcpToolCallResult Query(string toolName, string text)
        {
            return Query(toolName, new JArray(new JObject { ["type"] = "text", ["text"] = text ?? string.Empty }));
        }

        public static MapMcpToolCallResult Query(string toolName, JArray content)
        {
            var text = string.Join("\n", content.OfType<JObject>().Where(block => block["type"]?.ToString() == "text")
                .Select(block => block["text"]?.ToString() ?? string.Empty));
            return new MapMcpToolCallResult
            {
                ToolName = toolName,
                Success = !text.TrimStart().StartsWith("Error:", StringComparison.OrdinalIgnoreCase) &&
                    !text.TrimStart().StartsWith("# ERROR", StringComparison.OrdinalIgnoreCase),
                IsQuery = true,
                Text = text,
                Content = (JArray)content.DeepClone()
            };
        }

        public static MapMcpToolCallResult Action(string toolName, string command, string executionResult = null)
        {
            return new MapMcpToolCallResult
            {
                ToolName = toolName,
                Success = true,
                Text = string.IsNullOrWhiteSpace(executionResult)
                    ? $"Command staged: {command}"
                    : executionResult,
                Command = command
            }.WithTextContent();
        }

        public static MapMcpToolCallResult Error(string toolName, string text)
        {
            return new MapMcpToolCallResult
            {
                ToolName = toolName,
                Success = false,
                Text = text
            }.WithTextContent();
        }

        private MapMcpToolCallResult WithTextContent()
        {
            Content = new JArray(new JObject { ["type"] = "text", ["text"] = Text ?? string.Empty });
            return this;
        }
    }
}
