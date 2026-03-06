using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
        private const int DEFAULT_POLL_TIMEOUT_SECONDS = 180;
        private const int DEFAULT_POLL_INTERVAL_MS = 250;
        private const int NPM_INSTALL_TIMEOUT_MS = 120000;

        private static readonly string[] OPEN_CODE_BUILTIN_TOOL_DENYLIST =
        {
            "question",
            "bash",
            "read",
            "glob",
            "grep",
            "edit",
            "write",
            "task",
            "webfetch",
            "todowrite",
            "websearch",
            "codesearch",
            "skill",
            "apply_patch"
        };

        private static readonly string[] OPEN_CODE_CLI_CANDIDATES =
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "opencode.cmd"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "opencode"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "opencode", "opencode.exe"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "opencode")
        };

        private static bool _definitionsRecordToolsPayloadSupported = true;
        private static bool _definitionsArrayToolsPayloadSupported = true;
        private static readonly object _toolSyncLock = new object();
        private static DateTime _lastToolSyncUtc = DateTime.MinValue;
        private const int TOOL_SYNC_COOLDOWN_SECONDS = 3;

        private readonly string host;
        private readonly int port;
        private readonly string model;
        private readonly string defaultReasoningEffort;
        private readonly string directory;
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

        private sealed class ToolPayloadCandidate
        {
            public string Mode { get; set; }
            public JToken Payload { get; set; }
        }

        private sealed class ServerToolCallRecord
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public JObject Arguments { get; set; }
            public JToken Result { get; set; }
            public bool IsError { get; set; }
        }

        private sealed class OpenCodeApiException : Exception
        {
            public int? StatusCode { get; }
            public string ResponseBody { get; }

            public OpenCodeApiException(string message, int? statusCode = null, string responseBody = null, Exception innerException = null)
                : base(message, innerException)
            {
                StatusCode = statusCode;
                ResponseBody = responseBody;
            }
        }

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
        /// <param name="reasoningEffort">Optional default reasoning effort (low/medium/high/xhigh)</param>
        public OpenCodeClient(string host = null, int port = 0, string model = null, bool autoStart = true, string reasoningEffort = null)
        {
            this.host = string.IsNullOrEmpty(host) ? DEFAULT_HOST : host;
            this.port = port > 0 ? port : DEFAULT_PORT;
            this.model = model;
            this.defaultReasoningEffort = NormalizeReasoningEffort(reasoningEffort);
            var configuredDirectory = Environment.GetEnvironmentVariable("OPENCODE_DIRECTORY");
            var resolvedDirectory = OpenCodeToolGenerator.ResolveProjectRoot(configuredDirectory);
            if (string.IsNullOrWhiteSpace(resolvedDirectory) || !System.IO.Directory.Exists(resolvedDirectory))
            {
                resolvedDirectory = OpenCodeToolGenerator.ResolveProjectRoot(null);
            }

            this.directory = resolvedDirectory;
            this.autoStart = autoStart;
            this.baseUrl = $"http://{this.host}:{this.port}";
            Debug.WriteLine($"[OpenCode] Using project directory: {this.directory}");

            // Auto-regenerate TypeScript tools if C# definitions have changed
            try
            {
                SyncToolWrappers(forceRegenerate: false);
            }
            catch (Exception ex)
            {
                // Silently ignore - tools may not be needed or WZ data not loaded yet
                Debug.WriteLine($"[OpenCode] Auto-regenerate skipped: {ex.Message}");
            }

            // Start the AI Tool Server so OpenCode's server-side tools can call back to HaCreator
            StartToolServer();
        }

        private void SyncToolWrappers(bool forceRegenerate)
        {
            lock (_toolSyncLock)
            {
                OpenCodeToolGenerator.EnsureWorkspaceScaffold(directory);

                if (!forceRegenerate &&
                    (DateTime.UtcNow - _lastToolSyncUtc).TotalSeconds < TOOL_SYNC_COOLDOWN_SECONDS)
                {
                    return;
                }

                if (forceRegenerate)
                {
                    var generated = OpenCodeToolGenerator.RegenerateTools(directory);
                    Debug.WriteLine($"[OpenCode] Tool wrappers regenerated ({generated} files) in {directory}");
                }
                else
                {
                    var regenerated = OpenCodeToolGenerator.AutoRegenerateIfNeeded(directory);
                    if (regenerated)
                    {
                        Debug.WriteLine($"[OpenCode] Tool wrappers synchronized in {directory}");
                    }
                }

                EnsureToolDependenciesInstalled();

                _lastToolSyncUtc = DateTime.UtcNow;
            }
        }

        private void EnsureToolDependenciesInstalled()
        {
            var workspaceDir = System.IO.Path.Combine(directory, ".opencode");
            var pluginModuleDir = System.IO.Path.Combine(workspaceDir, "node_modules", "@opencode-ai", "plugin");
            EnsureWorkspacePackageJson(workspaceDir);

            if (System.IO.Directory.Exists(pluginModuleDir))
            {
                return;
            }

            if (!IsNodeInstalled())
            {
                throw new Exception("Node.js is required to install OpenCode tool dependencies. Install Node.js from https://nodejs.org/");
            }

            if (!IsNpmInstalled())
            {
                throw new Exception("npm is required to install OpenCode tool dependencies. Ensure npm is available in PATH.");
            }

            if (!System.IO.Directory.Exists(workspaceDir))
            {
                System.IO.Directory.CreateDirectory(workspaceDir);
            }

            OnServerStatusChanged?.Invoke("Installing OpenCode tool dependencies...");
            Debug.WriteLine($"[OpenCode] Installing tool dependencies in {workspaceDir}");

            var npmInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npm install --no-audit --no-fund",
                WorkingDirectory = workspaceDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var npm = Process.Start(npmInfo))
            {
                if (npm == null)
                {
                    throw new Exception("Failed to launch npm for OpenCode tool dependency installation.");
                }

                var stdout = npm.StandardOutput.ReadToEnd();
                var stderr = npm.StandardError.ReadToEnd();

                if (!npm.WaitForExit(NPM_INSTALL_TIMEOUT_MS))
                {
                    try { npm.Kill(); } catch { }
                    throw new Exception("Timed out while installing OpenCode tool dependencies via npm.");
                }

                if (npm.ExitCode != 0)
                {
                    var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                    throw new Exception($"npm install failed for OpenCode tools (exit {npm.ExitCode}). {details}");
                }
            }

            if (!System.IO.Directory.Exists(pluginModuleDir))
            {
                throw new Exception("OpenCode tool dependencies were installed but '@opencode-ai/plugin' is still missing.");
            }
        }

        private static void EnsureWorkspacePackageJson(string workspaceDir)
        {
            if (string.IsNullOrWhiteSpace(workspaceDir))
            {
                return;
            }

            System.IO.Directory.CreateDirectory(workspaceDir);

            var packageJsonPath = System.IO.Path.Combine(workspaceDir, "package.json");
            JObject packageJson;

            if (System.IO.File.Exists(packageJsonPath))
            {
                try
                {
                    packageJson = JObject.Parse(System.IO.File.ReadAllText(packageJsonPath));
                }
                catch
                {
                    packageJson = new JObject();
                }
            }
            else
            {
                packageJson = new JObject();
            }

            var dependencies = packageJson["dependencies"] as JObject ?? new JObject();
            dependencies["@opencode-ai/plugin"] = "latest";
            packageJson["dependencies"] = dependencies;

            System.IO.File.WriteAllText(
                packageJsonPath,
                packageJson.ToString(Formatting.Indented),
                Encoding.UTF8);
        }

        private static List<string> GetRequiredToolNames()
        {
            return MapEditorFunctions.GetToolDefinitions()
                .OfType<JObject>()
                .Select(tool => tool["function"]?["name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();
        }

        private async Task EnsureServerToolSyncAsync()
        {
            var requiredToolNames = GetRequiredToolNames();
            if (requiredToolNames.Count == 0)
            {
                return;
            }

            var registeredToolIds = await ListRegisteredToolIdsAsync();
            if (registeredToolIds == null || registeredToolIds.Count == 0)
            {
                // Older OpenCode builds may not expose /experimental/tool/ids; local wrapper sync is still done.
                return;
            }

            var registeredSet = new HashSet<string>(registeredToolIds, StringComparer.Ordinal);
            var missing = requiredToolNames.Where(name => !registeredSet.Contains(name)).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            Debug.WriteLine($"[OpenCode] Missing registered tools detected ({missing.Count}). Forcing wrapper sync.");
            OnServerStatusChanged?.Invoke("Synchronizing OpenCode tools...");
            SyncToolWrappers(forceRegenerate: true);

            if (IsManagedServerRunning)
            {
                Debug.WriteLine("[OpenCode] Restarting managed OpenCode server to reload synchronized tools.");
                StopServer();
                var restarted = await StartServerAsync();
                if (restarted)
                {
                    registeredToolIds = await ListRegisteredToolIdsAsync();
                    registeredSet = new HashSet<string>(registeredToolIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                    missing = requiredToolNames.Where(name => !registeredSet.Contains(name)).ToList();
                }
            }

            if (missing.Count > 0)
            {
                Debug.WriteLine($"[OpenCode] Tool registration still missing: {string.Join(", ", missing)}");
                OnServerStatusChanged?.Invoke("OpenCode tools synchronized. Restart server to reload tool wrappers.");
            }
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
            SyncToolWrappers(forceRegenerate: false);

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

                var cliPath = ResolveOpenCodeCliPath();
                if (string.IsNullOrWhiteSpace(cliPath))
                {
                    OnServerStatusChanged?.Invoke(
                        "OpenCode CLI not found. Set OPENCODE_CLI_PATH or install with: npm install -g opencode");
                    return false;
                }

                if (IsNodeRequiredForCli(cliPath) && !IsNodeInstalled())
                {
                    OnServerStatusChanged?.Invoke(
                        $"Node.js is required to run OpenCode CLI at '{cliPath}'. Install Node.js from https://nodejs.org/");
                    return false;
                }

                OnServerStatusChanged?.Invoke("Starting OpenCode server...");

                try
                {
                    var startInfo = BuildOpenCodeStartInfo(cliPath);

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

                    try
                    {
                        _serverProcess.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                                Debug.WriteLine($"[OpenCode][stdout] {e.Data}");
                        };
                        _serverProcess.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                                Debug.WriteLine($"[OpenCode][stderr] {e.Data}");
                        };
                        _serverProcess.BeginOutputReadLine();
                        _serverProcess.BeginErrorReadLine();
                    }
                    catch
                    {
                        // Non-fatal; server may still run even if async output hooks fail.
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    OnServerStatusChanged?.Invoke(
                        "OpenCode CLI launch failed. Set OPENCODE_CLI_PATH or install with: npm install -g opencode");
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

        private ProcessStartInfo BuildOpenCodeStartInfo(string cliPath)
        {
            var escapedCliPath = cliPath.Contains(" ") ? $"\"{cliPath}\"" : cliPath;
            var serveArgs = $"serve --port {port} --hostname {host}";
            var extension = System.IO.Path.GetExtension(cliPath) ?? string.Empty;

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var serverCwd = Environment.GetEnvironmentVariable("OPENCODE_SERVER_CWD");
            if (string.IsNullOrWhiteSpace(serverCwd))
            {
                serverCwd = System.IO.Path.Combine(directory, ".opencode");
            }

            if (!string.IsNullOrWhiteSpace(serverCwd))
            {
                if (!System.IO.Directory.Exists(serverCwd))
                {
                    System.IO.Directory.CreateDirectory(serverCwd);
                }
                startInfo.WorkingDirectory = serverCwd;
            }

            if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c {escapedCliPath} {serveArgs}";
            }
            else
            {
                startInfo.FileName = cliPath;
                startInfo.Arguments = serveArgs;
            }

            return startInfo;
        }

        private static bool IsNodeRequiredForCli(string cliPath)
        {
            var extension = (System.IO.Path.GetExtension(cliPath) ?? string.Empty).ToLowerInvariant();
            if (extension == ".cmd" || extension == ".bat" || extension == ".js")
                return true;

            var lower = cliPath.ToLowerInvariant();
            return lower.Contains(@"\npm\") || lower.EndsWith("/npm/opencode", StringComparison.Ordinal);
        }

        private static string ResolveOpenCodeCliPath()
        {
            bool IsPreferredCliPath(string path)
            {
                var ext = (System.IO.Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
                return ext == ".cmd" || ext == ".bat" || ext == ".exe";
            }

            var envPath = (Environment.GetEnvironmentVariable("OPENCODE_CLI_PATH") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                if (System.IO.File.Exists(envPath))
                {
                    if (!IsPreferredCliPath(envPath))
                    {
                        var cmdCandidate = envPath + ".cmd";
                        if (System.IO.File.Exists(cmdCandidate))
                            return cmdCandidate;
                    }
                    return envPath;
                }

                var expanded = Environment.ExpandEnvironmentVariables(envPath);
                if (System.IO.File.Exists(expanded))
                {
                    if (!IsPreferredCliPath(expanded))
                    {
                        var cmdCandidate = expanded + ".cmd";
                        if (System.IO.File.Exists(cmdCandidate))
                            return cmdCandidate;
                    }
                    return expanded;
                }
            }

            try
            {
                var whereInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "opencode",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(whereInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(3000);
                        if (process.ExitCode == 0)
                        {
                            var candidates = output
                                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(line => line.Trim())
                                .Where(line => !string.IsNullOrWhiteSpace(line) && System.IO.File.Exists(line))
                                .ToList();

                            var preferred = candidates.FirstOrDefault(IsPreferredCliPath);
                            if (!string.IsNullOrWhiteSpace(preferred))
                                return preferred;

                            var fallback = candidates.FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(fallback))
                                return fallback;
                        }
                    }
                }
            }
            catch
            {
                // Ignore PATH probe failures and continue with known candidates.
            }

            foreach (var candidate in OPEN_CODE_CLI_CANDIDATES)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && System.IO.File.Exists(candidate))
                    return candidate;
            }

            return null;
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

        private static bool IsNpmInstalled()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npm --version",
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
            SyncToolWrappers(forceRegenerate: false);

            if (await IsServerRunningAsync())
            {
                await EnsureServerToolSyncAsync();
                return;
            }

            if (autoStart)
            {
                var started = await StartServerAsync();
                if (!started)
                {
                    throw new Exception(
                        $"OpenCode server not running at {baseUrl} and failed to auto-start. " +
                        "Please run 'opencode serve' manually. See https://opencode.ai/docs/server/");
                }

                await EnsureServerToolSyncAsync();
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
        /// Directory used for OpenCode project-scoped requests (directory query parameter).
        /// </summary>
        public string ProjectDirectory => directory;

        /// <summary>
        /// List registered OpenCode tool IDs for the current project directory.
        /// Returns null when the server build does not expose /experimental/tool/ids.
        /// </summary>
        public async Task<List<string>> GetRegisteredToolIdsForCurrentDirectoryAsync(bool ensureServer = true)
        {
            if (ensureServer)
            {
                await EnsureServerAsync();
            }

            return await ListRegisteredToolIdsAsync();
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

                try
                {
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
                catch (Exception ex)
                {
                    // If another HaCreator instance already owns the tool callback port,
                    // keep OpenCode usable for prompt/session operations in this process.
                    if (ex.Message != null &&
                        ex.Message.IndexOf("conflicts with an existing registration", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _toolServer = null;
                        Debug.WriteLine("[OpenCode] Tool server port is already registered by another process. Continuing without local callback server.");
                        return;
                    }

                    throw;
                }
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
                var assistantNarrative = new List<string>();
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

                    var turnText = NormalizeAssistantNarrative(ExtractTextContent(response));
                    if (!string.IsNullOrWhiteSpace(turnText) &&
                        !assistantNarrative.Contains(turnText, StringComparer.Ordinal))
                    {
                        assistantNarrative.Add(turnText);
                    }

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

                        var followUpText = NormalizeAssistantNarrative(ExtractTextContent(response));
                        if (!string.IsNullOrWhiteSpace(followUpText) &&
                            !assistantNarrative.Contains(followUpText, StringComparer.Ordinal))
                        {
                            assistantNarrative.Add(followUpText);
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
                    Debug.WriteLine("[OpenCode] No commands generated - model returned no actionable tool calls or command text");
                    if (assistantNarrative.Count > 0)
                    {
                        return string.Join(Environment.NewLine + Environment.NewLine, assistantNarrative);
                    }

                    // Final fallback: request a direct natural-language response so chat UI
                    // still shows meaningful output even when no tools/commands were emitted.
                    try
                    {
                        var fallbackPrompt = BuildNoCommandFallbackPrompt(mapContext, userInstructions);
                        var directResponse = await SendSimplePromptAsync(
                            "You are assisting a MapleStory map editor user. If no executable commands are needed, respond conversationally and concisely.",
                            fallbackPrompt);

                        if (!string.IsNullOrWhiteSpace(directResponse))
                        {
                            return directResponse.Trim();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OpenCode] Direct fallback response failed: {ex.Message}");
                    }

                    return "I could not generate executable map commands for that request. Try asking for specific map edits, or ask a follow-up question.";
                }

                var finalResult = string.Join(Environment.NewLine, allCommands);
                if (assistantNarrative.Count > 0)
                {
                    finalResult = string.Join(
                        Environment.NewLine + Environment.NewLine,
                        assistantNarrative.Concat(new[] { finalResult }));
                }

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

        private static bool GetEnvBool(string name, bool defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "y":
                case "on":
                    return true;
                case "0":
                case "false":
                case "no":
                case "n":
                case "off":
                    return false;
                default:
                    return defaultValue;
            }
        }

        private static int GetEnvInt(string name, int defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
        }

        private static double GetEnvDouble(string name, double defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
        }

        private static HashSet<string> GetEnvCsvSet(string name, string defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(raw))
                raw = defaultValue;

            return new HashSet<string>(
                raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrEmpty(item)),
                StringComparer.Ordinal);
        }

        private static JObject BuildModelSpecObject(string rawModel)
        {
            if (string.IsNullOrWhiteSpace(rawModel))
                return null;

            var trimmed = rawModel.Trim();
            string providerId;
            string modelId;

            if (trimmed.Contains("/"))
            {
                var split = trimmed.Split(new[] { '/' }, 2);
                providerId = split[0];
                modelId = split[1];
            }
            else
            {
                var lower = trimmed.ToLowerInvariant();
                if (lower.StartsWith("gpt-") || lower.StartsWith("o1") || lower.StartsWith("o3") || lower.StartsWith("o4"))
                    providerId = "openai";
                else if (lower.Contains("gemini"))
                    providerId = "google";
                else if (lower.Contains("claude"))
                    providerId = "anthropic";
                else if (lower.Contains("grok"))
                    providerId = "xai";
                else
                    providerId = "anthropic";

                modelId = trimmed;
            }

            return new JObject
            {
                ["providerID"] = providerId,
                ["modelID"] = modelId
            };
        }

        private static string NormalizeReasoningEffort(string reasoningEffort)
        {
            if (string.IsNullOrWhiteSpace(reasoningEffort))
                return null;

            var normalized = reasoningEffort.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "low":
                case "medium":
                case "high":
                case "xhigh":
                    return normalized;
                default:
                    return null;
            }
        }

        private void ApplyCommonMessageOptions(JObject body, string systemPrompt, float? temperature, int? thinkingBudget, string reasoningEffort)
        {
            if (temperature.HasValue)
            {
                body["temperature"] = temperature.Value;
            }

            if (thinkingBudget.HasValue && thinkingBudget.Value > 0)
            {
                body["thinking"] = new JObject
                {
                    ["type"] = "enabled",
                    ["budgetTokens"] = thinkingBudget.Value
                };
            }

            var effectiveReasoningEffort = NormalizeReasoningEffort(reasoningEffort) ?? defaultReasoningEffort;
            if (!string.IsNullOrWhiteSpace(effectiveReasoningEffort))
            {
                body["reasoningEffort"] = effectiveReasoningEffort;
            }

            if (!string.IsNullOrWhiteSpace(model))
            {
                var modelSpec = BuildModelSpecObject(model);
                if (modelSpec != null)
                {
                    body["model"] = modelSpec;
                }
            }

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                body["system"] = systemPrompt;
            }
        }

        private static JObject ConvertToolsToPermissionsMap(JArray tools)
        {
            var permissions = AIToolConverter.ToOpenCodeBooleanFormat(tools ?? new JArray());

            if (GetEnvBool("OPENCODE_DISABLE_BUILTIN_TOOLS", true))
            {
                foreach (var builtinTool in OPEN_CODE_BUILTIN_TOOL_DENYLIST)
                {
                    permissions[builtinTool] = false;
                }
            }

            return permissions;
        }

        private static JObject ConvertToolsToDefinitionRecord(JArray tools)
        {
            var result = new JObject();
            var simpleTools = AIToolConverter.ToSimpleFormat(tools ?? new JArray());

            for (var i = 0; i < simpleTools.Count; i++)
            {
                var tool = simpleTools[i] as JObject;
                if (tool == null)
                    continue;

                var toolName = tool["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    toolName = $"tool_{i}";
                }

                var def = new JObject();
                if (tool["description"] != null)
                    def["description"] = tool["description"];
                if (tool["parameters"] != null)
                    def["parameters"] = tool["parameters"];
                if (tool["inputSchema"] != null)
                    def["inputSchema"] = tool["inputSchema"];
                if (tool["required"] is JArray required)
                    def["required"] = required;
                if (tool["strict"]?.Type == JTokenType.Boolean)
                    def["strict"] = tool["strict"];

                if (!def.HasValues)
                {
                    def["enabled"] = true;
                }

                result[toolName] = def;
            }

            return result;
        }

        private static JObject BuildAllowlistedPermissionsMap(JObject basePermissions, IEnumerable<string> allowedToolNames, IEnumerable<string> registeredToolNames)
        {
            var permissions = new JObject();
            foreach (var property in (basePermissions ?? new JObject()).Properties())
            {
                permissions[property.Name] = property.Value.Type == JTokenType.Boolean && property.Value.Value<bool>();
            }

            var allowed = new HashSet<string>(
                (allowedToolNames ?? Enumerable.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.Ordinal);

            foreach (var registeredTool in (registeredToolNames ?? Enumerable.Empty<string>()))
            {
                if (string.IsNullOrWhiteSpace(registeredTool))
                    continue;
                permissions[registeredTool] = allowed.Contains(registeredTool);
            }

            foreach (var allowedTool in allowed)
            {
                permissions[allowedTool] = true;
            }

            return permissions;
        }

        private static List<ToolPayloadCandidate> BuildToolsPayloadCandidates(JArray tools)
        {
            var simpleTools = AIToolConverter.ToSimpleFormat(tools ?? new JArray());

            var permissions = new ToolPayloadCandidate
            {
                Mode = "permissions",
                Payload = ConvertToolsToPermissionsMap(simpleTools)
            };

            var definitionsRecord = new ToolPayloadCandidate
            {
                Mode = "definitions_record",
                Payload = ConvertToolsToDefinitionRecord(simpleTools)
            };

            var definitionsArray = new ToolPayloadCandidate
            {
                Mode = "definitions_array",
                Payload = simpleTools
            };

            var payloadMode = (Environment.GetEnvironmentVariable("OPENCODE_TOOLS_PAYLOAD_MODE") ?? "permissions")
                .Trim()
                .ToLowerInvariant();

            if (payloadMode != "definitions" && payloadMode != "permissions" && payloadMode != "auto")
            {
                payloadMode = "auto";
            }

            var candidates = new List<ToolPayloadCandidate>();
            if (payloadMode == "permissions")
            {
                candidates.Add(permissions);
                candidates.Add(definitionsRecord);
                if (simpleTools.Count > 0)
                    candidates.Add(definitionsArray);
            }
            else if (payloadMode == "definitions")
            {
                candidates.Add(definitionsRecord);
                if (simpleTools.Count > 0)
                    candidates.Add(definitionsArray);
                candidates.Add(permissions);
            }
            else
            {
                candidates.Add(definitionsRecord);
                candidates.Add(permissions);
                if (simpleTools.Count > 0)
                    candidates.Add(definitionsArray);
            }

            if (!_definitionsRecordToolsPayloadSupported)
            {
                candidates = candidates.Where(candidate => candidate.Mode != "definitions_record").ToList();
            }

            if (!_definitionsArrayToolsPayloadSupported)
            {
                candidates = candidates.Where(candidate => candidate.Mode != "definitions_array").ToList();
            }

            return candidates.Count > 0 ? candidates : new List<ToolPayloadCandidate> { permissions };
        }

        private static bool IsRetryableToolPayloadStatus(int? statusCode)
        {
            if (!statusCode.HasValue)
                return false;

            return statusCode.Value == 400 || statusCode.Value == 404 || statusCode.Value == 405 || statusCode.Value == 422;
        }

        private static void UpdateToolPayloadSupportFlags(ToolPayloadCandidate candidate, OpenCodeApiException apiException)
        {
            if (candidate == null || apiException == null || !apiException.StatusCode.HasValue || apiException.StatusCode.Value != 400)
                return;

            var payloadText = (apiException.ResponseBody ?? apiException.Message ?? string.Empty).ToLowerInvariant();

            if (candidate.Mode == "definitions_record" && payloadText.Contains("expected boolean, received object"))
            {
                _definitionsRecordToolsPayloadSupported = false;
            }

            if (candidate.Mode == "definitions_array" && payloadText.Contains("expected record, received array"))
            {
                _definitionsArrayToolsPayloadSupported = false;
            }
        }

        private async Task<List<string>> ListRegisteredToolIdsAsync()
        {
            try
            {
                var response = await SendRequestAsync("GET", "/experimental/tool/ids");
                var items = response?["items"] as JArray;
                if (items == null)
                    return null;

                var ids = new List<string>();
                foreach (var item in items)
                {
                    if (item.Type == JTokenType.String)
                    {
                        var toolId = item.ToString().Trim();
                        if (!string.IsNullOrEmpty(toolId))
                            ids.Add(toolId);
                    }
                }
                return ids;
            }
            catch (OpenCodeApiException apiEx) when (apiEx.StatusCode == 400 || apiEx.StatusCode == 404 || apiEx.StatusCode == 405 || apiEx.StatusCode == 422 || apiEx.StatusCode == 501)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<JArray> ListMessagesAsync(string sessionId, int limit = 50)
        {
            var endpoint = $"/session/{sessionId}/message?limit={Math.Max(1, limit)}";
            var response = await SendRequestAsync("GET", endpoint);

            if (response?["items"] is JArray items)
                return items;

            if (response?["messages"] is JArray messages)
                return messages;

            return new JArray();
        }

        private async Task<HashSet<string>> CaptureMessageIdsAsync(string sessionId, int limit = 100)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var messages = await ListMessagesAsync(sessionId, limit);
            foreach (var message in messages.OfType<JObject>())
            {
                var id = message["info"]?["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        private async Task<JObject> WaitForMessageAfterPromptAsync(string sessionId, HashSet<string> previousIds, int timeoutSeconds = 0)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            var pollTimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : GetEnvInt("OPENCODE_POLL_TIMEOUT_SECONDS", DEFAULT_POLL_TIMEOUT_SECONDS);
            var pollIntervalSeconds = GetEnvDouble("OPENCODE_POLL_INTERVAL_SECONDS", 0.25);
            var pollIntervalMs = Math.Max(50, (int)(pollIntervalSeconds * 1000));
            var deadline = DateTime.UtcNow.AddSeconds(pollTimeoutSeconds);
            JObject lastAssistantMessage = null;

            while (DateTime.UtcNow < deadline)
            {
                JArray messages;
                try
                {
                    messages = await ListMessagesAsync(sessionId, 100);
                }
                catch
                {
                    await Task.Delay(pollIntervalMs);
                    continue;
                }

                foreach (var message in messages.OfType<JObject>().Reverse())
                {
                    var info = message["info"] as JObject;
                    if (info?["role"]?.ToString() != "assistant")
                        continue;

                    var id = info["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id) && previousIds != null && previousIds.Contains(id))
                        continue;

                    lastAssistantMessage = message;
                    var parts = message["parts"] as JArray;
                    if ((parts != null && parts.Count > 0) || info["error"] != null)
                    {
                        return message;
                    }
                }

                await Task.Delay(pollIntervalMs > 0 ? pollIntervalMs : DEFAULT_POLL_INTERVAL_MS);
            }

            return lastAssistantMessage;
        }

        private static bool ResponseHasAssistantPayload(JObject response)
        {
            if (response == null)
                return false;

            var parts = response["parts"] as JArray;
            if (parts != null && parts.Count > 0)
                return true;

            var toolCalls = response["tool_calls"] as JArray;
            return toolCalls != null && toolCalls.Count > 0;
        }

        private async Task<JObject> SendSessionMessageWithRecoveryAsync(string sessionId, JObject body)
        {
            HashSet<string> previousIds = null;
            try
            {
                previousIds = await CaptureMessageIdsAsync(sessionId, 100);
            }
            catch
            {
                // Message-listing is best effort only.
            }

            try
            {
                var response = await SendRequestAsync("POST", $"/session/{sessionId}/message", body);
                if (ResponseHasAssistantPayload(response))
                    return response;

                var recovered = await WaitForMessageAfterPromptAsync(sessionId, previousIds);
                if (recovered != null)
                    return recovered;

                var pollTimeoutSeconds = GetEnvInt("OPENCODE_POLL_TIMEOUT_SECONDS", DEFAULT_POLL_TIMEOUT_SECONDS);
                throw new OpenCodeApiException(
                    $"No assistant response received within polling window ({pollTimeoutSeconds}s). " +
                    "This may indicate a slow model response or OpenCode server timeout.");
            }
            catch (Exception ex) when (previousIds != null)
            {
                var recoverTimeout = GetEnvInt("OPENCODE_NETWORK_RECOVER_POLL_TIMEOUT_SECONDS", GetEnvInt("OPENCODE_POLL_TIMEOUT_SECONDS", DEFAULT_POLL_TIMEOUT_SECONDS));
                var recovered = await WaitForMessageAfterPromptAsync(sessionId, previousIds, recoverTimeout);
                if (recovered != null)
                {
                    Debug.WriteLine($"[OpenCode] Recovered assistant response after failed /message POST: {ex.Message}");
                    return recovered;
                }
                throw;
            }
        }

        private async Task<JObject> SendMessageWithToolPayloadFallbackAsync(string sessionId, JObject bodyBase, JArray tools)
        {
            if (tools == null || tools.Count == 0)
            {
                return await SendSessionMessageWithRecoveryAsync(sessionId, bodyBase);
            }

            var toolCandidates = BuildToolsPayloadCandidates(tools);
            var simpleTools = AIToolConverter.ToSimpleFormat(tools);
            var allowedToolNames = simpleTools.OfType<JObject>()
                .Select(tool => tool["name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            var registeredToolIds = await ListRegisteredToolIdsAsync();
            if (registeredToolIds != null && registeredToolIds.Count > 0)
            {
                foreach (var candidate in toolCandidates.Where(candidate => candidate.Mode == "permissions"))
                {
                    if (candidate.Payload is JObject basePermissions)
                    {
                        candidate.Payload = BuildAllowlistedPermissionsMap(basePermissions, allowedToolNames, registeredToolIds);
                    }
                }
            }

            OpenCodeApiException lastApiError = null;
            Exception lastError = null;

            for (int i = 0; i < toolCandidates.Count; i++)
            {
                var candidate = toolCandidates[i];
                var body = (JObject)bodyBase.DeepClone();
                body["tools"] = candidate.Payload;

                try
                {
                    var response = await SendSessionMessageWithRecoveryAsync(sessionId, body);
                    if (i > 0)
                    {
                        Debug.WriteLine($"[OpenCode] Tool payload fallback succeeded using mode '{candidate.Mode}'");
                    }
                    return response;
                }
                catch (OpenCodeApiException apiEx)
                {
                    lastApiError = apiEx;
                    lastError = apiEx;
                    UpdateToolPayloadSupportFlags(candidate, apiEx);

                    var isLast = i >= toolCandidates.Count - 1;
                    if (!isLast && IsRetryableToolPayloadStatus(apiEx.StatusCode))
                    {
                        var nextMode = toolCandidates[i + 1].Mode;
                        Debug.WriteLine($"[OpenCode] Tool payload mode '{candidate.Mode}' rejected (status={apiEx.StatusCode}), retrying with '{nextMode}'");
                        continue;
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (i < toolCandidates.Count - 1)
                    {
                        Debug.WriteLine($"[OpenCode] Tool payload mode '{candidate.Mode}' failed: {ex.Message}. Retrying with fallback.");
                        continue;
                    }
                }
            }

            if (lastApiError != null)
                throw lastApiError;
            if (lastError != null)
                throw lastError;

            throw new Exception("Failed to send OpenCode message: no tool payload mode was accepted");
        }

        /// <summary>
        /// Send a message to a session with tools.
        /// </summary>
        private async Task<JObject> SendMessageAsync(
            string sessionId,
            string text,
            JArray tools,
            string systemPrompt = null,
            bool requireTools = false,
            float? temperature = null,
            int? thinkingBudget = null,
            string reasoningEffort = null)
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
            ApplyCommonMessageOptions(body, systemPrompt, temperature, thinkingBudget, reasoningEffort);

            // Log the full request (truncated for readability)
            var bodyStr = body.ToString(Formatting.None);
            Debug.WriteLine($"[OpenCode] Request body (first 3000 chars): {bodyStr.Substring(0, Math.Min(3000, bodyStr.Length))}");

            return await SendMessageWithToolPayloadFallbackAsync(sessionId, body, tools);
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
            var candidateParts = new List<JArray>
            {
                new JArray
                {
                    new JObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = toolCallId,
                        ["tool_call_id"] = toolCallId,
                        ["content"] = result,
                        ["is_error"] = isError
                    }
                },
                new JArray
                {
                    new JObject
                    {
                        ["type"] = "tool-result",
                        ["toolUseID"] = toolCallId,
                        ["toolCallID"] = toolCallId,
                        ["content"] = result,
                        ["isError"] = isError
                    }
                },
                new JArray
                {
                    new JObject
                    {
                        ["type"] = "tool",
                        ["id"] = toolCallId,
                        ["content"] = result,
                        ["is_error"] = isError
                    }
                }
            };

            Exception lastError = null;
            foreach (var parts in candidateParts)
            {
                var body = new JObject
                {
                    ["parts"] = parts
                };

                try
                {
                    var response = await SendSessionMessageWithRecoveryAsync(sessionId, body);
                    Debug.WriteLine($"[OpenCode] send_tool_result accepted payload type '{parts[0]?["type"]}' for call_id={toolCallId}");
                    return response;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.WriteLine($"[OpenCode] send_tool_result payload type '{parts[0]?["type"]}' failed for call_id={toolCallId}: {ex.Message}");
                }
            }

            throw lastError ?? new Exception($"Failed to send tool result for call_id={toolCallId}");
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

            var parts = response["parts"] as JArray;
            if (parts != null)
            {
                foreach (var part in parts.OfType<JObject>())
                {
                    if (part["type"]?.ToString() != "text")
                        continue;

                    var text = part["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text) && !text.StartsWith("#") && text.Length > 10)
                    {
                        commands.Add($"# {text.Substring(0, Math.Min(100, text.Length))}...");
                    }
                }
            }

            var toolCalls = ExtractToolCalls(response, null, true);
            if (toolCalls.Count == 0)
            {
                var error = response["error"]?.ToString();
                if (!string.IsNullOrEmpty(error))
                {
                    return (new List<string> { $"# Error: {error}" }, toolResponses, false);
                }

                if (commands.Count > 0)
                    return (commands, toolResponses, false);

                var text = ExtractTextContent(response);
                if (!string.IsNullOrEmpty(text))
                {
                    commands.Add("# AI Response:");
                    commands.Add(text);
                }
                return (commands, toolResponses, false);
            }

            foreach (var toolCall in toolCalls)
            {
                var toolCallId = toolCall.Id ?? Guid.NewGuid().ToString();
                var functionName = toolCall.Name;
                var arguments = toolCall.RawArguments ?? new JObject();

                Debug.WriteLine($"[OpenCode] Tool call: {functionName}, args: {arguments.ToString(Formatting.None)}");

                if (string.IsNullOrWhiteSpace(functionName))
                    continue;

                try
                {
                    if (MapEditorFunctions.IsQueryFunction(functionName))
                    {
                        hasQueryFunctions = true;
                        _calledQueryFunctions.Add(functionName);
                        commands.Add($"# QUERY: {functionName} called");
                        var queryResult = MapEditorFunctions.ExecuteQueryFunction(functionName, arguments);
                        Debug.WriteLine($"[OpenCode] Query {functionName} result: {queryResult?.Substring(0, Math.Min(500, queryResult?.Length ?? 0))}");
                        toolResponses.Add((toolCallId, queryResult, false));
                    }
                    else
                    {
                        var requiredQuery = MapEditorFunctions.GetRequiredQuery(functionName);
                        var command = MapEditorFunctions.FunctionCallToCommand(functionName, arguments);

                        if (requiredQuery != null && !_calledQueryFunctions.Contains(requiredQuery))
                        {
                            commands.Add($"# WARNING: {functionName} called without {requiredQuery}");
                            commands.Add(command);
                            toolResponses.Add((toolCallId, $"Command queued: {functionName} (WARNING: {requiredQuery} was not called first)", false));
                        }
                        else
                        {
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

            return (commands, toolResponses, hasQueryFunctions);
        }

        private string NormalizeAssistantNarrative(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var lines = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line =>
                    !line.StartsWith("ADD ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("SET ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("DELETE ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("MOVE ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("TILE ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("CLEAR ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("FLIP ", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("# QUERY:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("# WARNING:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lines.Count == 0)
                return null;

            return string.Join(Environment.NewLine, lines);
        }

        private string BuildNoCommandFallbackPrompt(string mapContext, string userInstructions)
        {
            var context = string.IsNullOrWhiteSpace(mapContext)
                ? "(map context unavailable)"
                : mapContext;

            var request = string.IsNullOrWhiteSpace(userInstructions)
                ? "(no user instruction provided)"
                : userInstructions;

            return
                "The map-edit tool pipeline did not produce any executable commands.\n" +
                "Provide a helpful direct response to the user instead.\n\n" +
                "User request:\n" + request + "\n\n" +
                "Current map context:\n" + context;
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

                ApplyCommonMessageOptions(body, systemPrompt, temperature: null, thinkingBudget: null, reasoningEffort: null);
                var response = await SendSessionMessageWithRecoveryAsync(sessionId, body);
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

        private string AppendDirectoryQuery(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("directory=", StringComparison.OrdinalIgnoreCase))
                return endpoint;

            if (endpoint.StartsWith("/global/", StringComparison.OrdinalIgnoreCase) ||
                endpoint.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }

            var encodedDirectory = Uri.EscapeDataString(directory ?? string.Empty);
            return endpoint.Contains("?")
                ? $"{endpoint}&directory={encodedDirectory}"
                : $"{endpoint}?directory={encodedDirectory}";
        }

        /// <summary>
        /// Make HTTP request to OpenCode server.
        /// </summary>
        private async Task<JObject> SendRequestAsync(string method, string endpoint, JObject body = null)
        {
            var endpointWithDirectory = AppendDirectoryQuery(endpoint);
            var url = $"{baseUrl}{endpointWithDirectory}";
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
                    throw new OpenCodeApiException(
                        $"OpenCode API error: {response.StatusCode} - {errorDetail}",
                        (int)response.StatusCode,
                        responseContent);
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
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var numValue))
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
                throw new OpenCodeApiException($"OpenCode request timed out after {REQUEST_TIMEOUT_MS / 1000} seconds");
            }
            catch (HttpRequestException ex)
            {
                throw new OpenCodeApiException($"OpenCode connection failed: {ex.Message}. Is the server running at {baseUrl}?", null, null, ex);
            }
            catch (JsonReaderException ex)
            {
                // Log and include the actual response for debugging
                Debug.WriteLine($"[OpenCode] JSON parse error: {ex.Message}");
                Debug.WriteLine($"[OpenCode] Response content: {responseContent}");
                throw new OpenCodeApiException($"OpenCode returned invalid JSON: {ex.Message}. Response was: {responseContent?.Substring(0, Math.Min(500, responseContent?.Length ?? 0))}", null, responseContent, ex);
            }
        }

        #region High-Level Tool Execution API

        /// <summary>
        /// Extract tool calls from an OpenCode response.
        /// Handles multiple provider/tool-call shapes, including OpenCode server-side records.
        /// </summary>
        /// <param name="response">The raw response from OpenCode</param>
        /// <param name="allowedToolNames">Optional allowlist of tool names</param>
        /// <param name="allowTextFallback">Whether to parse text-form tool call fallback blocks</param>
        /// <returns>List of extracted tool calls</returns>
        public List<OpenCodeToolCall> ExtractToolCalls(
            JObject response,
            IEnumerable<string> allowedToolNames = null,
            bool allowTextFallback = true)
        {
            var toolCalls = new List<OpenCodeToolCall>();
            if (response == null)
                return toolCalls;

            var allowed = new HashSet<string>(
                (allowedToolNames ?? Enumerable.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.Ordinal);

            bool IsAllowed(string toolName)
            {
                return allowed.Count == 0 || allowed.Contains(toolName);
            }

            void AddToolCall(string id, string name, JToken argumentsToken)
            {
                if (string.IsNullOrWhiteSpace(name) || !IsAllowed(name))
                    return;

                var rawArgs = CoerceToolArguments(argumentsToken);

                var toolCall = new OpenCodeToolCall
                {
                    Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id,
                    Name = name,
                    RawArguments = rawArgs,
                    Arguments = new Dictionary<string, object>()
                };

                foreach (var prop in rawArgs.Properties())
                {
                    toolCall.Arguments[prop.Name] = prop.Value.ToObject<object>();
                }

                toolCalls.Add(toolCall);
            }

            if (response["tool_calls"] is JArray directCalls)
            {
                foreach (var call in directCalls.OfType<JObject>())
                {
                    var function = call["function"] as JObject;
                    var name = call["name"]?.ToString() ?? function?["name"]?.ToString();
                    var argsToken = call["arguments"] ?? function?["arguments"];
                    AddToolCall(call["id"]?.ToString(), name, argsToken);
                }

                if (toolCalls.Count > 0)
                    return toolCalls;
            }

            var parts = response["parts"] as JArray;
            if (parts != null)
            {
                foreach (var part in parts.OfType<JObject>())
                {
                    var partType = (part["type"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    var function = part["function"] as JObject;

                    switch (partType)
                    {
                        case "tool_use":
                        case "tool-use":
                            AddToolCall(part["id"]?.ToString(), part["name"]?.ToString(), part["input"] ?? part["arguments"]);
                            break;
                        case "tool-invocation":
                        case "tool_invocation":
                        case "tool_call":
                        case "tool-call":
                            AddToolCall(part["id"]?.ToString(), part["name"]?.ToString() ?? function?["name"]?.ToString(),
                                part["args"] ?? part["arguments"] ?? function?["arguments"]);
                            break;
                        case "function_call":
                            AddToolCall(part["id"]?.ToString(), part["name"]?.ToString() ?? function?["name"]?.ToString(),
                                part["arguments"] ?? function?["arguments"]);
                            break;
                        case "tool":
                            var state = part["state"] as JObject;
                            AddToolCall(
                                part["callID"]?.ToString() ?? part["call_id"]?.ToString() ?? part["id"]?.ToString(),
                                part["tool"]?.ToString() ?? part["name"]?.ToString(),
                                state?["input"] ?? part["input"] ?? part["arguments"]);
                            break;
                    }
                }
            }

            if (toolCalls.Count > 0 || !allowTextFallback)
                return toolCalls;

            var textParts = new List<string>();
            foreach (var part in parts?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                if (part["type"]?.ToString() == "text")
                {
                    var text = part["text"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        textParts.Add(text);
                }
            }

            if (textParts.Count == 0)
                return toolCalls;

            foreach (var fallbackCall in ExtractTextFallbackToolCalls(string.Join("\n", textParts), allowed))
            {
                toolCalls.Add(fallbackCall);
            }

            return toolCalls;
        }

        private static JObject CoerceToolArguments(JToken argumentsToken)
        {
            if (argumentsToken == null || argumentsToken.Type == JTokenType.Null)
                return new JObject();

            if (argumentsToken is JObject obj)
                return (JObject)obj.DeepClone();

            if (argumentsToken.Type == JTokenType.String)
            {
                var raw = argumentsToken.ToString().Trim();
                if (string.IsNullOrWhiteSpace(raw))
                    return new JObject();

                try
                {
                    var parsed = JToken.Parse(raw);
                    if (parsed is JObject parsedObj)
                        return parsedObj;
                }
                catch
                {
                    // Continue with key/value fallback parsing.
                }

                var fallback = new JObject();
                foreach (Match match in Regex.Matches(raw, "([A-Za-z0-9_]+)\\s*:\\s*(\".*?\"|'.*?'|[^,\\n]+)"))
                {
                    var key = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        fallback[key] = value;
                    }
                }
                return fallback;
            }

            if (argumentsToken.Type == JTokenType.Object)
            {
                return (JObject)argumentsToken;
            }

            return new JObject();
        }

        private static List<OpenCodeToolCall> ExtractTextFallbackToolCalls(string text, HashSet<string> allowedToolNames)
        {
            var calls = new List<OpenCodeToolCall>();
            if (string.IsNullOrWhiteSpace(text))
                return calls;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int i = 0;
            int sequence = 0;

            bool IsAllowed(string name)
            {
                return allowedToolNames == null || allowedToolNames.Count == 0 || allowedToolNames.Contains(name);
            }

            while (i < lines.Length)
            {
                var raw = lines[i].Trim();
                var toolMatch = Regex.Match(raw, "^`?([A-Za-z_][A-Za-z0-9_-]*)`?$");
                if (!toolMatch.Success)
                {
                    i++;
                    continue;
                }

                var toolName = toolMatch.Groups[1].Value;
                if (!IsAllowed(toolName))
                {
                    i++;
                    continue;
                }

                var args = new JObject();
                i++;

                while (i < lines.Length)
                {
                    var current = lines[i].Trim();
                    if (string.IsNullOrEmpty(current))
                    {
                        i++;
                        if (args.HasValues)
                            break;
                        continue;
                    }

                    if (Regex.IsMatch(current, "^`?[A-Za-z_][A-Za-z0-9_-]*`?$"))
                    {
                        break;
                    }

                    var kv = Regex.Match(current, "^-?\\s*([A-Za-z_][A-Za-z0-9_]*)\\s*:\\s*(.*)$");
                    if (kv.Success)
                    {
                        var key = kv.Groups[1].Value.Trim();
                        var value = kv.Groups[2].Value.Trim();
                        if ((value.StartsWith("`") && value.EndsWith("`")) || (value.StartsWith("\"") && value.EndsWith("\"")))
                        {
                            value = value.Substring(1, Math.Max(0, value.Length - 2)).Trim();
                        }
                        args[key] = value;
                    }

                    i++;
                }

                sequence++;
                var toolCall = new OpenCodeToolCall
                {
                    Id = $"text_call_{sequence}",
                    Name = toolName,
                    RawArguments = args,
                    Arguments = args.Properties().ToDictionary(prop => prop.Name, prop => prop.Value.ToObject<object>())
                };
                calls.Add(toolCall);
            }

            return calls;
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
        /// <param name="temperature">Optional temperature setting</param>
        /// <param name="thinkingBudget">Optional thinking budget for extended thinking (Claude)</param>
        /// <param name="reasoningEffort">Optional reasoning effort for OpenAI/Grok reasoning models</param>
        /// <returns>Raw response from OpenCode</returns>
        public async Task<JObject> SendPromptWithImagesAsync(
            string sessionId,
            string text,
            JArray tools = null,
            List<string> images = null,
            string systemPrompt = null,
            float? temperature = null,
            int? thinkingBudget = null,
            string reasoningEffort = null)
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
            ApplyCommonMessageOptions(body, systemPrompt, temperature, thinkingBudget, reasoningEffort);

            return await SendMessageWithToolPayloadFallbackAsync(sessionId, body, tools);
        }

        /// <summary>
        /// Extract server-side tool call records from session message history.
        /// Some OpenCode builds execute tools on the server and only expose calls in timeline parts of type "tool".
        /// </summary>
        private async Task<List<ServerToolCallRecord>> ExtractServerToolCallsFromSessionAsync(
            string sessionId,
            IEnumerable<string> allowedToolNames = null,
            int limit = 100)
        {
            var records = new List<ServerToolCallRecord>();
            var allowed = new HashSet<string>(
                (allowedToolNames ?? Enumerable.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            JArray messages;
            try
            {
                messages = await ListMessagesAsync(sessionId, limit);
            }
            catch
            {
                return records;
            }

            foreach (var message in messages.OfType<JObject>())
            {
                var info = message["info"] as JObject;
                if (info?["role"]?.ToString() != "assistant")
                    continue;

                if (!(message["parts"] is JArray parts))
                    continue;

                foreach (var part in parts.OfType<JObject>())
                {
                    var partType = (part["type"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (partType != "tool")
                        continue;

                    var toolName = part["tool"]?.ToString() ?? part["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(toolName))
                        continue;

                    if (allowed.Count > 0 && !allowed.Contains(toolName))
                        continue;

                    var state = part["state"] as JObject;
                    var args = CoerceToolArguments(state?["input"] ?? part["input"] ?? part["arguments"]);
                    var callId = part["callID"]?.ToString()
                        ?? part["call_id"]?.ToString()
                        ?? part["tool_call_id"]?.ToString()
                        ?? part["id"]?.ToString()
                        ?? $"server_call_{records.Count + 1}";
                    var dedupeKey = $"{callId}|{toolName}|{args.ToString(Formatting.None)}";
                    if (!seen.Add(dedupeKey))
                        continue;

                    var status = (state?["status"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    var isError = status == "error" || (state?["error"] != null);

                    JToken result = null;
                    if (state?["output"] != null)
                        result = state["output"];
                    else if (state?["result"] != null)
                        result = state["result"];
                    else if (state?["error"] != null)
                        result = new JObject { ["error"] = state["error"] };

                    records.Add(new ServerToolCallRecord
                    {
                        Id = callId,
                        Name = toolName,
                        Arguments = args,
                        Result = result,
                        IsError = isError
                    });
                }
            }

            return records;
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
        /// <param name="temperature">Optional temperature setting</param>
        /// <param name="reasoningEffort">Optional reasoning effort for OpenAI/Grok reasoning models</param>
        /// <returns>Result containing final text and all tool calls made</returns>
        public async Task<RunWithToolsResult> RunWithToolsAsync(
            string text,
            JArray tools,
            Func<string, JObject, Task<object>> toolExecutor,
            List<string> images = null,
            string systemPrompt = null,
            string sessionTitle = "tool-session",
            int maxIterations = 10,
            int? thinkingBudget = null,
            float? temperature = null,
            string reasoningEffort = null)
        {
            // Ensure server is running
            await EnsureServerAsync();

            var result = new RunWithToolsResult
            {
                ToolCallsMade = new List<OpenCodeToolCallResult>()
            };

            var simpleTools = AIToolConverter.ToSimpleFormat(tools ?? new JArray());
            var allowedToolNames = simpleTools
                .OfType<JObject>()
                .Select(tool => tool["name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();
            var allowedToolNameSet = new HashSet<string>(allowedToolNames, StringComparer.Ordinal);
            var ignoredUnlistedTools = GetEnvCsvSet("OPENCODE_IGNORED_UNLISTED_TOOLS", "invalid");
            var strictAllowedToolCalls = GetEnvBool("OPENCODE_STRICT_ALLOWED_TOOL_CALLS", true);
            var allowServerToolErrors = GetEnvBool("OPENCODE_ALLOW_SERVER_TOOL_ERRORS", false);

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
                    sessionId,
                    text,
                    simpleTools,
                    images,
                    systemPrompt,
                    temperature,
                    thinkingBudget,
                    reasoningEffort);

                Debug.WriteLine($"[OpenCode] RunWithTools initial response received");

                bool foundToolCalls = false;

                // Tool execution loop
                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    var toolCalls = ExtractToolCalls(response, allowedToolNames, allowTextFallback: false);

                    if (toolCalls.Count == 0)
                    {
                        var serverToolCallsAll = await ExtractServerToolCallsFromSessionAsync(sessionId, null, 100);
                        if (serverToolCallsAll.Count > 0)
                        {
                            var disallowedServerCalls = serverToolCallsAll
                                .Where(call => !string.IsNullOrWhiteSpace(call.Name)
                                    && !allowedToolNameSet.Contains(call.Name)
                                    && !ignoredUnlistedTools.Contains(call.Name))
                                .ToList();
                            if (strictAllowedToolCalls && disallowedServerCalls.Count > 0)
                            {
                                var disallowedNames = string.Join(", ", disallowedServerCalls.Select(call => call.Name).Distinct());
                                throw new Exception($"OpenCode executed disallowed server-side tools: {disallowedNames}");
                            }
                        }

                        var allowedServerCalls = serverToolCallsAll
                            .Where(call => !string.IsNullOrWhiteSpace(call.Name) && allowedToolNameSet.Contains(call.Name))
                            .ToList();

                        if (allowedServerCalls.Count > 0)
                        {
                            var serverErrorCalls = allowedServerCalls.Where(call => call.IsError).ToList();
                            if (!allowServerToolErrors && serverErrorCalls.Count > 0)
                            {
                                var failingNames = string.Join(", ", serverErrorCalls.Select(call => call.Name).Distinct());
                                var firstError = serverErrorCalls[0].Result?["error"]?.ToString()
                                    ?? serverErrorCalls[0].Result?["message"]?.ToString()
                                    ?? serverErrorCalls[0].Result?.ToString();
                                throw new Exception(
                                    $"OpenCode server-side tool execution failed for: {failingNames}" +
                                    (string.IsNullOrWhiteSpace(firstError) ? string.Empty : $" (sample error: {firstError})"));
                            }

                            foreach (var serverCall in allowedServerCalls)
                            {
                                result.ToolCallsMade.Add(new OpenCodeToolCallResult
                                {
                                    Name = serverCall.Name,
                                    Arguments = serverCall.Arguments.Properties().ToDictionary(prop => prop.Name, prop => prop.Value.ToObject<object>()),
                                    Result = serverCall.Result?.ToObject<object>(),
                                    IsError = serverCall.IsError
                                });
                            }

                            result.Iterations = iteration + 1;
                            break;
                        }

                        toolCalls = ExtractToolCalls(response, allowedToolNames, allowTextFallback: true);
                    }

                    if (toolCalls.Count > 0 && allowedToolNameSet.Count > 0)
                    {
                        var disallowedToolCalls = toolCalls
                            .Where(call => !string.IsNullOrWhiteSpace(call.Name)
                                && !allowedToolNameSet.Contains(call.Name)
                                && !ignoredUnlistedTools.Contains(call.Name))
                            .ToList();

                        if (strictAllowedToolCalls && disallowedToolCalls.Count > 0)
                        {
                            var disallowedNames = string.Join(", ", disallowedToolCalls.Select(call => call.Name).Distinct());
                            throw new Exception($"OpenCode requested disallowed tools: {disallowedNames}");
                        }

                        toolCalls = toolCalls
                            .Where(call => !string.IsNullOrWhiteSpace(call.Name) && allowedToolNameSet.Contains(call.Name))
                            .ToList();
                    }

                    if (toolCalls.Count == 0)
                    {
                        // No more tool calls, we're done
                        Debug.WriteLine($"[OpenCode] RunWithTools completed after {iteration} iterations");
                        break;
                    }

                    Debug.WriteLine($"[OpenCode] RunWithTools iteration {iteration + 1}: {toolCalls.Count} tool calls");
                    foundToolCalls = true;

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
                if (!foundToolCalls && result.Iterations == 0)
                {
                    result.Iterations = 1;
                }
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
            int? thinkingBudget = null,
            float? temperature = null,
            string reasoningEffort = null)
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
                thinkingBudget,
                temperature,
                reasoningEffort);
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
            int? thinkingBudget = null,
            float? temperature = null,
            string reasoningEffort = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpenCodeSession));

            return await _client.SendPromptWithImagesAsync(
                _sessionId,
                text,
                tools,
                images,
                systemPrompt,
                temperature,
                thinkingBudget,
                reasoningEffort);
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
