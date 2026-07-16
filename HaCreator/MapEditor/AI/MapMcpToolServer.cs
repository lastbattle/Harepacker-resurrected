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
                    result["additionalProperties"] = false;
                    result["required"] = new JArray(properties.Properties().Select(property => property.Name));

                    foreach (var property in properties.Properties().ToList())
                    {
                        if (property.Value is JObject propertySchema)
                            property.Value = CreateStrictSchema(propertySchema);
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

            if (!GetMcpTools().OfType<JObject>().Any(t => string.Equals(
                    t["name"]?.ToString(), toolName, StringComparison.Ordinal)))
            {
                return MapMcpToolCallResult.Error(toolName, $"Unknown map tool: {toolName}");
            }

            try
            {
                if (MapEditorFunctions.IsQueryFunction(toolName))
                {
                    lock (queryLock)
                    {
                        calledQueries.Add(toolName);
                    }

                    var query = QueryExecutor != null
                        ? QueryExecutor(toolName, arguments)
                        : MapEditorFunctions.ExecuteQueryFunction(toolName, arguments);
                    return MapMcpToolCallResult.Query(toolName, query ?? string.Empty);
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
                            ["content"] = new JArray
                            {
                                new JObject { ["type"] = "text", ["text"] = call.Text ?? string.Empty }
                            }
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

        public static MapMcpToolCallResult Query(string toolName, string text)
        {
            return new MapMcpToolCallResult
            {
                ToolName = toolName,
                Success = true,
                IsQuery = true,
                Text = text
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
            };
        }

        public static MapMcpToolCallResult Error(string toolName, string text)
        {
            return new MapMcpToolCallResult
            {
                ToolName = toolName,
                Success = false,
                Text = text
            };
        }
    }
}
