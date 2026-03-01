using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// HTTP server that handles tool execution requests from OpenCode's server-side tools.
    /// OpenCode TypeScript tools call this server to execute map editing commands.
    /// </summary>
    public class AIToolServer : IDisposable
    {
        private const int DEFAULT_PORT = 19840;
        private const string LOCALHOST = "127.0.0.1";

        private readonly HttpListener _listener;
        private readonly int _port;
        private CancellationTokenSource _cts;
        private Task _serverTask;
        private bool _isRunning;

        /// <summary>
        /// Callback to execute query functions (get_mob_list, get_object_info, etc.)
        /// </summary>
        public Func<string, JObject, string> QueryExecutor { get; set; }

        /// <summary>
        /// Callback to execute action functions and convert them to commands
        /// </summary>
        public Func<string, JObject, string> ActionExecutor { get; set; }

        /// <summary>
        /// Event raised when a command is received and ready for execution
        /// </summary>
        public event EventHandler<string> CommandReceived;

        public AIToolServer(int port = DEFAULT_PORT)
        {
            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{LOCALHOST}:{_port}/");
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;

        /// <summary>
        /// Start the HTTP server
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            try
            {
                _listener.Start();
                _isRunning = true;
                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => ListenLoop(_cts.Token));
                Debug.WriteLine($"[AIToolServer] Started on port {_port}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIToolServer] Failed to start: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stop the HTTP server
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _cts?.Cancel();
            _listener.Stop();
            _isRunning = false;
            Debug.WriteLine("[AIToolServer] Stopped");
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    // Server was stopped
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener was disposed
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIToolServer] Listen error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Set CORS headers for local development
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                // Handle CORS preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                if (request.HttpMethod != "POST" || request.Url.AbsolutePath != "/tool")
                {
                    await SendErrorResponse(response, 404, "Not Found");
                    return;
                }

                // Read request body
                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    body = await reader.ReadToEndAsync();
                }

                Debug.WriteLine($"[AIToolServer] Received: {body.Substring(0, Math.Min(500, body.Length))}");

                // Parse request
                JObject requestJson;
                try
                {
                    requestJson = JObject.Parse(body);
                }
                catch (JsonException ex)
                {
                    await SendErrorResponse(response, 400, $"Invalid JSON: {ex.Message}");
                    return;
                }

                var toolName = requestJson["tool"]?.ToString();
                var arguments = requestJson["arguments"] as JObject ?? new JObject();

                if (string.IsNullOrEmpty(toolName))
                {
                    await SendErrorResponse(response, 400, "Missing 'tool' field");
                    return;
                }

                // Execute the tool
                string result;
                try
                {
                    result = ExecuteTool(toolName, arguments);
                }
                catch (Exception ex)
                {
                    await SendErrorResponse(response, 500, $"Tool execution error: {ex.Message}");
                    return;
                }

                // Send success response
                await SendJsonResponse(response, 200, result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIToolServer] Request error: {ex.Message}");
                try
                {
                    await SendErrorResponse(response, 500, $"Internal error: {ex.Message}");
                }
                catch { }
            }
        }

        private string ExecuteTool(string toolName, JObject arguments)
        {
            Debug.WriteLine($"[AIToolServer] Executing tool: {toolName}");

            // Check if it's a query function
            if (MapEditorFunctions.IsQueryFunction(toolName))
            {
                string queryResult;
                if (QueryExecutor != null)
                {
                    queryResult = QueryExecutor(toolName, arguments);
                }
                else
                {
                    // Default: use MapEditorFunctions.ExecuteQueryFunction
                    queryResult = MapEditorFunctions.ExecuteQueryFunction(toolName, arguments);
                }

                Debug.WriteLine($"[AIToolServer] Query result length: {queryResult?.Length ?? 0}");

                // Wrap in JSON for proper response
                return JsonConvert.SerializeObject(new { success = true, message = queryResult });
            }
            else
            {
                // Action function - convert to command and notify
                string command;
                if (ActionExecutor != null)
                {
                    command = ActionExecutor(toolName, arguments);
                }
                else
                {
                    // Default: use MapEditorFunctions.FunctionCallToCommand
                    command = MapEditorFunctions.FunctionCallToCommand(toolName, arguments);
                }

                Debug.WriteLine($"[AIToolServer] Command: {command}");

                // Raise event so the UI can execute the command
                CommandReceived?.Invoke(this, command);

                return JsonConvert.SerializeObject(new { success = true, command = command, message = $"Executed: {command}" });
            }
        }

        private async Task SendJsonResponse(HttpListenerResponse response, int statusCode, string json)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";

            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message)
        {
            var errorJson = JsonConvert.SerializeObject(new { error = message });
            await SendJsonResponse(response, statusCode, errorJson);
        }

        public void Dispose()
        {
            Stop();
            _listener?.Close();
            _cts?.Dispose();
        }
    }
}
