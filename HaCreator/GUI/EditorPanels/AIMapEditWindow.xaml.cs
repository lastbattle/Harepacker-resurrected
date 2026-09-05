using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// WPF Window for AI-based map editing with chat-style interface.
    /// One instance is created per map/board.
    /// </summary>
    public partial class AIMapEditWindow : Window
    {
        private static readonly Dictionary<Board, AIMapEditWindow> instances = new Dictionary<Board, AIMapEditWindow>();

        private readonly Board board;
        private readonly ChatSession _chatSession;
        private readonly MapMcpToolServer mapMcpServer;
        private bool isProcessing = false;
        private CancellationTokenSource requestCancellation;
        private JObject previewMetadata;
        private HaCreator.MapEditor.UndoRedo.UndoRedoBatch lastSessionUndo;

        /// <summary>
        /// Loopback MCP endpoint for the active map window.
        /// </summary>
        public string McpEndpoint => mapMcpServer?.Endpoint;
        public string McpAuthorizationToken => mapMcpServer?.AuthorizationToken;
        public event EventHandler<string> McpCommandReceived;

        private AIMapEditWindow(Board board)
        {
            this.board = board;

            // Initialize chat session
            _chatSession = new ChatSession();
            mapMcpServer = new MapMcpToolServer();
            mapMcpServer.CommandReceived += (sender, command) =>
            {
                if (Dispatcher.CheckAccess())
                    McpCommandReceived?.Invoke(this, command);
                else
                    Dispatcher.BeginInvoke(new Action(() => McpCommandReceived?.Invoke(this, command)));
            };
            mapMcpServer.CommandExecutor = ApplyMcpCommand;
            mapMcpServer.RichQueryExecutor = QueryMap;

            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            RefreshApplyMode();

            // Bind chat messages to ItemsControl
            chatItemsControl.ItemsSource = _chatSession.Messages;

            // Subscribe to collection changes for auto-scroll
            _chatSession.Messages.CollectionChanged += Messages_CollectionChanged;

            // Update title with map info
            UpdateTitle();

            // Start the external MCP endpoint only after the WPF window is initialized.
            mapMcpServer.Start();
        }

        private void UpdateTitle()
        {
            string mapName = EditorPanelLocalizer.Text("AI_UnknownMap", "Unknown");
            int mapId = 0;

            if (board?.MapInfo != null)
            {
                mapId = board.MapInfo.id;
                mapName = !string.IsNullOrEmpty(board.MapInfo.strMapName)
                    ? board.MapInfo.strMapName
                    : EditorPanelLocalizer.Format("AI_DefaultMapName", mapId);
            }

            this.Title = EditorPanelLocalizer.Format("AI_WindowTitle", mapName, mapId);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Hide instead of close to preserve state
            e.Cancel = true;
            this.Hide();
        }

        #region Static Instance Management

        /// <summary>
        /// Get or create an AI Map Edit window for the specified board
        /// </summary>
        public static AIMapEditWindow GetOrCreate(Board board)
        {
            if (board == null)
                return null;

            // Clean up any closed instances
            CleanupClosedInstances();

            if (instances.TryGetValue(board, out var existingWindow))
            {
                return existingWindow;
            }

            var newWindow = new AIMapEditWindow(board);
            instances[board] = newWindow;
            return newWindow;
        }

        /// <summary>
        /// Show the AI Map Edit window for the specified board
        /// </summary>
        public static void ShowForBoard(Board board, Window owner = null)
        {
            var window = GetOrCreate(board);
            if (window == null)
            {
                MessageBox.Show(EditorPanelLocalizer.Text("AI_NoMapLoaded", "No map is currently loaded."), EditorPanelLocalizer.Text("AI Map Editor"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (owner != null)
            {
                window.Owner = owner;
            }

            if (window.IsVisible)
            {
                window.Activate();
                window.Focus();
            }
            else
            {
                window.Show();
            }

            // Auto-load map context every time the window is shown/focused
            window.LoadMapContext();
        }

        /// <summary>
        /// Close and dispose the window for a specific board
        /// </summary>
        public static void CloseForBoard(Board board)
        {
            if (board != null && instances.TryGetValue(board, out var window))
            {
                instances.Remove(board);
                window.Closing -= window.Window_Closing;
                window.requestCancellation?.Cancel();
                window.Close();
                window.mapMcpServer?.Dispose();
            }
        }

        /// <summary>
        /// Close all AI Map Edit windows and cleanup resources
        /// </summary>
        public static void CloseAll()
        {
            foreach (var window in instances.Values)
            {
                window.Closing -= window.Window_Closing;
                window.requestCancellation?.Cancel();
                window.Close();
                window.mapMcpServer?.Dispose();
            }
            instances.Clear();

        }

        private static void CleanupClosedInstances()
        {
            var toRemove = new List<Board>();
            foreach (var kvp in instances)
            {
                try
                {
                    var _ = kvp.Value.IsVisible;
                }
                catch
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
            {
                instances.Remove(key);
            }
        }

        #endregion

        #region Map Context

        /// <summary>
        /// Loads the current map context into the description panel.
        /// </summary>
        public void LoadMapContext()
        {
            if (board == null)
            {
                txtMapContext.Text = EditorPanelLocalizer.Text("AI_NoMapContext", "# No map loaded");
                return;
            }

            try
            {
                var serializer = new MapAISerializer(board);
                var text = serializer.GenerateAISummary();
                txtMapContext.Text = text;

                // Update chat session with current map context
                _chatSession.CurrentMapContext = text;
                RefreshVisualContext();
            }
            catch (Exception ex)
            {
                txtMapContext.Text = EditorPanelLocalizer.Format("AI_MapLoadError", ex.Message);
            }
        }

        private JArray QueryMap(string name, JObject args)
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(() => QueryMap(name, args));
            lock (board.ParentControl)
            {
                return name switch
                {
                    "get_map_state" => new JArray(new JObject { ["type"] = "text", ["text"] = new MapAISerializer(board).GenerateSpatialState(args) }),
                    "get_map_view" => MapAIVisualRenderer.RenderMap(board, args),
                    "get_asset_preview" => MapAIVisualRenderer.RenderAssets(args),
                    _ => new JArray(new JObject { ["type"] = "text", ["text"] = MapEditorFunctions.ExecuteQueryFunction(name, args) })
                };
            }
        }

        private JArray RefreshVisualContext()
        {
            var args = new JObject();
            var state = JObject.Parse(new MapAISerializer(board).GenerateSpatialState(new JObject { ["limit"] = 1 }));
            if (state["globalGeometryBounds"] is JArray bounds)
            {
                const int padding = 64;
                args["x"] = bounds[0].Value<long>() - padding;
                args["y"] = bounds[1].Value<long>() - padding;
                args["width"] = Math.Max(1, bounds[2].Value<long>() - bounds[0].Value<long>() + padding * 2);
                args["height"] = Math.Max(1, bounds[3].Value<long>() - bounds[1].Value<long>() + padding * 2);
            }
            var content = MapAIVisualRenderer.RenderMap(board, args);
            previewMetadata = JObject.Parse(content.OfType<JObject>().First(b => b["type"]?.ToString() == "text")["text"].ToString());
            var image = content.OfType<JObject>().FirstOrDefault(b => b["type"]?.ToString() == "image");
            if (image != null)
            {
                using var stream = new MemoryStream(Convert.FromBase64String(image["data"].ToString()));
                var source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.StreamSource = stream;
                source.EndInit();
                source.Freeze();
                imgMapPreview.Source = source;
            }
            return content;
        }

        private void MapPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (previewMetadata == null || imgMapPreview.Source is not BitmapSource source) return;
            double zoom = Math.Min(imgMapPreview.ActualWidth / source.PixelWidth, imgMapPreview.ActualHeight / source.PixelHeight);
            if (zoom <= 0) return;
            var point = e.GetPosition(imgMapPreview);
            double pixelX = (point.X - (imgMapPreview.ActualWidth - source.PixelWidth * zoom) / 2) / zoom;
            double pixelY = (point.Y - (imgMapPreview.ActualHeight - source.PixelHeight * zoom) / 2) / zoom;
            if (pixelX < 0 || pixelY < 0 || pixelX >= source.PixelWidth || pixelY >= source.PixelHeight) return;
            double scale = previewMetadata["pixelsPerWorldUnit"].Value<double>();
            int x = (int)Math.Round(previewMetadata["worldBounds"]["x"].Value<double>() + pixelX / scale);
            int y = (int)Math.Round(previewMetadata["worldBounds"]["y"].Value<double>() + pixelY / scale);
            string coordinate = $" at ({x}, {y}) ";
            int insertionStart = txtMessageInput.SelectionStart;
            txtMessageInput.SelectedText = coordinate;
            txtMessageInput.CaretIndex = insertionStart + coordinate.Length;
            txtMessageInput.Focus();
        }

        #endregion

        #region Chat Event Handlers

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll to bottom when new messages added
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                chatScrollViewer.ScrollToEnd();
            }));
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private void TxtMessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Enter sends message, Shift+Enter adds new line
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                _ = SendMessageAsync(); // Fire and forget for UI responsiveness
            }
        }

        private void TxtMessageInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Enable/disable send button based on input
            btnSend.IsEnabled = !string.IsNullOrWhiteSpace(txtMessageInput.Text) && !isProcessing;
        }

        private async Task SendMessageAsync()
        {
            if (isProcessing)
            {
                return;
            }

            var userInput = txtMessageInput.Text?.Trim();
            if (string.IsNullOrEmpty(userInput))
            {
                return;
            }

            if (!EnsureAIConfiguration())
            {
                return;
            }

            if (board == null)
            {
                MessageBox.Show(EditorPanelLocalizer.Text("AI_NoMapLoaded", "No map is currently loaded."), EditorPanelLocalizer.Text("AI Map Editor"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                isProcessing = true;
                lastSessionUndo = null;
                requestCancellation = new CancellationTokenSource();
                btnSend.IsEnabled = false;
                btnExecute.IsEnabled = false;
                btnStop.IsEnabled = true;
                btnClearChat.IsEnabled = false;
                chkLiveEdits.IsEnabled = false;
                var history = _chatSession.ToConversationHistory();
                bool applyChanges = AISettings.AutoApplyCommands;

                // Clear input
                txtMessageInput.Clear();

                // Add user message to chat
                _chatSession.AddUserMessage(userInput);

                // Add placeholder assistant message (shows "Thinking...")
                var assistantMessage = _chatSession.AddAssistantMessage();

                // Update map context
                var serializer = new MapAISerializer(board);
                _chatSession.CurrentMapContext = serializer.GenerateAISummary();

                var visualContext = RefreshVisualContext();
                using var sessionTools = new MapMcpToolServer
                {
                    RichQueryExecutor = QueryMap,
                    CommandExecutor = applyChanges ? ApplySessionCommand : null
                };
                using var client = new OpenAICompatibleClient(AISettings.CreateMapEditorOptions(), sessionTools);
                client.Progress += status => Dispatcher.BeginInvoke(new Action(() => txtProgress.Text = status));
                client.ToolCompleted += result => Dispatcher.Invoke(() =>
                {
                    if (result.Success && !string.IsNullOrWhiteSpace(result.Command))
                    {
                        assistantMessage.CommandsContent += result.Command + Environment.NewLine;
                        assistantMessage.CommandsApplied = applyChanges;
                    }
                });
                string result = await client.ProcessConversationAsync(_chatSession.CurrentMapContext,
                    userInput, history, visualContext, applyChanges, requestCancellation.Token);

                // Parse response to separate explanation from commands
                var (explanation, commands) = ParseAIResponse(result);

                // Update assistant message
                assistantMessage.IsProcessing = false;
                assistantMessage.Content = explanation;
                // Only successful tool calls are executable. Prose that resembles a command is not.
                assistantMessage.CommandsApplied = applyChanges;
                txtProgress.Text = applyChanges ? "Finished • map refreshed" : "Ready to review • changes have not been applied";
                LoadMapContext();
            }
            catch (OperationCanceledException)
            {
                if (_chatSession.LastAssistantMessage != null)
                {
                    _chatSession.LastAssistantMessage.IsProcessing = false;
                    _chatSession.LastAssistantMessage.Content = "Stopped. Any changes already applied remain on the map and can be undone.";
                }
                txtProgress.Text = "Stopped";
            }
            catch (Exception ex)
            {
                if (_chatSession.LastAssistantMessage != null)
                {
                    _chatSession.LastAssistantMessage.IsProcessing = false;
                    _chatSession.LastAssistantMessage.HasError = true;
                    _chatSession.LastAssistantMessage.ErrorMessage = BuildAIErrorMessage(ex);
                }

                MaybeOpenAISettingsForError(ex);
                txtProgress.Text = "Request failed • see details in the conversation";
            }
            finally
            {
                isProcessing = false;
                requestCancellation?.Dispose();
                requestCancellation = null;
                btnStop.IsEnabled = false;
                btnClearChat.IsEnabled = true;
                chkLiveEdits.IsEnabled = true;
                btnSend.IsEnabled = !string.IsNullOrWhiteSpace(txtMessageInput.Text);
                btnExecute.IsEnabled = _chatSession.HasCommands;
                btnExecute.Visibility = _chatSession.HasCommands ? Visibility.Visible : Visibility.Collapsed;
                txtMessageInput.Focus();
            }
        }

        /// <summary>
        /// Parse AI response to separate explanation text from commands
        /// </summary>
        private (string explanation, string commands) ParseAIResponse(string response)
        {
            // Commands are lines starting with these prefixes
            var commandPrefixes = new[] {
                "ADD ", "SET ", "DELETE ", "MOVE ", "TILE ", "CLEAR ", "FLIP ",
                "# QUERY:", "# WARNING:"
            };

            var explanationLines = new List<string>();
            var commandLines = new List<string>();

            var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                bool isCommand = false;

                foreach (var prefix in commandPrefixes)
                {
                    if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        isCommand = true;
                        break;
                    }
                }

                if (isCommand)
                {
                    commandLines.Add(trimmed);
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    explanationLines.Add(trimmed);
                }
            }

            // If no explanation, provide a default
            string explanation = explanationLines.Count > 0
                ? string.Join(Environment.NewLine, explanationLines)
                : "Here are the commands to accomplish your request:";

            string commands = commandLines.Count > 0
                ? string.Join(Environment.NewLine, commandLines)
                : string.Empty;

            return (explanation, commands);
        }

        private void BtnClearChat_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing) return;
            if (_chatSession.HasMessages)
            {
                var result = MessageBox.Show(
                    EditorPanelLocalizer.Text("AI_ClearChatConfirm", "Start a new conversation? Current chat history will be cleared."),
                    EditorPanelLocalizer.Text("New Chat"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _chatSession.Clear();
                    btnExecute.IsEnabled = false;
                }
            }
        }

        #endregion

        #region Other Event Handlers

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadMapContext();
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing || !_chatSession.HasCommands) return;
            if (board == null)
            {
                MessageBox.Show(EditorPanelLocalizer.Text("AI_NoMapLoaded", "No map is currently loaded."), EditorPanelLocalizer.Text("AI_ExecuteCommandsTitle", "Execute Commands"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var commandText = _chatSession.GetLatestCommands();
            if (string.IsNullOrWhiteSpace(commandText))
            {
                MessageBox.Show(EditorPanelLocalizer.Text("AI_NoCommands", "No commands to execute. Send a message to generate commands first."),
                    EditorPanelLocalizer.Text("AI_ExecuteCommandsTitle", "Execute Commands"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var result = ExecuteCommandText(commandText);
                // Never replay a partially applied batch: retries must be generated from fresh state.
                _chatSession.LastAssistantMessage.CommandsApplied = true;
                btnExecute.IsEnabled = false;
                if (result == null)
                {
                    MessageBox.Show(EditorPanelLocalizer.Text("AI_NoValidCommands", "No valid commands found in the generated output."),
                        EditorPanelLocalizer.Text("AI_ExecuteCommandsTitle", "Execute Commands"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show execution summary
                string summary = EditorPanelLocalizer.Format("AI_ExecutionSummary", result.SuccessCount, result.FailCount);

                if (result.FailCount > 0 && result.Log.Count > 0)
                {
                    var failedLogs = result.Log.Where(l =>
                        l.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(5);
                    if (failedLogs.Any())
                    {
                        summary += "\n\nIssues:\n" + string.Join("\n", failedLogs);
                    }
                }

                MessageBox.Show(summary, EditorPanelLocalizer.Text("AI_ExecutionResultTitle", "Execution Result"),
                    MessageBoxButton.OK,
                    result.FailCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show(EditorPanelLocalizer.Format("AI_ExecutionError", ex.Message), EditorPanelLocalizer.Text("AI_ExecutionErrorTitle", "Execution Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ExecutionResult ExecuteCommandText(string commandText)
        {
            var parser = new MapAIParser();
            var commands = parser.ParseCommands(commandText);
            if (commands.Count == 0)
                return null;

            var executor = new MapAIExecutor(board);
            int undoCount = board.UndoRedoMan.UndoList.Count;
            var result = executor.ExecuteCommands(commands);
            if (result.SuccessCount > 0 || board.UndoRedoMan.UndoList.Count != undoCount)
            {
                board.Dirty = true;
                LoadMapContext();
            }

            return result;
        }

        private string ApplyMcpCommand(string commandText)
        {
            return Dispatcher.Invoke(() => isProcessing
                ? "# ERROR: The built-in AI is editing this map. Wait or stop it before external edits."
                : ApplySessionCommand(commandText));
        }

        private string ApplySessionCommand(string commandText)
        {
            ExecutionResult result = null;
            Exception error = null;

            Action apply = () =>
            {
                try
                {
                    requestCancellation?.Token.ThrowIfCancellationRequested();
                    var undo = board.UndoRedoMan;
                    int previousCount = undo.UndoList.Count;
                    bool followsSession = isProcessing && previousCount > 0 &&
                        ReferenceEquals(undo.UndoList[previousCount - 1], lastSessionUndo);
                    result = ExecuteCommandText(commandText);
                    if (isProcessing && undo.UndoList.Count > previousCount)
                    {
                        // Merge only consecutive AI operations. Never absorb a manual edit
                        // made in the map editor while the model was awaiting its next turn.
                        if (followsSession) undo.CollapseUndoBatches(previousCount - 1);
                        lastSessionUndo = undo.UndoList.Last();
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            };

            if (Dispatcher.CheckAccess())
                apply();
            else
                Dispatcher.Invoke(apply);

            if (error != null)
                return $"# ERROR: {error.Message}";
            if (result == null)
                return "# ERROR: The MCP command was not valid.";
            if (result.FailCount > 0)
                return $"# ERROR: {result.SuccessCount} succeeded, {result.FailCount} failed. {string.Join("; ", result.Log)}";

            return $"Applied {result.SuccessCount} map command(s). {string.Join("; ", result.Log)}";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => requestCancellation?.Cancel();

        private void RefreshApplyMode()
        {
            chkLiveEdits.IsChecked = AISettings.AutoApplyCommands;
            btnExecute.Visibility = _chatSession.HasCommands ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LiveEdits_Click(object sender, RoutedEventArgs e)
        {
            AISettings.AutoApplyCommands = chkLiveEdits.IsChecked == true;
            RefreshApplyMode();
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing || board.UndoRedoMan.UndoList.Count == 0) return;
            board.UndoRedoMan.Undo();
            board.Dirty = true;
            LoadMapContext();
            txtProgress.Text = "Undid the latest map operation";
        }

        private void BtnMcpConnection_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                EditorPanelLocalizer.Format("AI_McpConnectionDetails", McpEndpoint, McpAuthorizationToken),
                EditorPanelLocalizer.Text("AI_McpConnectionTitle", "MCP Connection"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnAISettings_Click(object sender, RoutedEventArgs e)
        {
            OpenAISettingsDialog();
        }

        private bool EnsureAIConfiguration()
        {
            if (AISettings.IsConfigured)
                return true;

            OpenAISettingsDialog();
            return AISettings.IsConfigured;
        }

        private void MaybeOpenAISettingsForError(Exception ex)
        {
            if (!AISettings.IsConfigurationRelatedError(ex))
                return;

            var result = MessageBox.Show(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    EditorPanelLocalizer.Text(
                        "AI_OpenSettingsAfterError",
                        "The AI request failed with a configuration or connection problem:\n\n{0}\n\nOpen AI Settings now?"),
                    ex.Message),
                EditorPanelLocalizer.Text("AI_OpenSettingsTitle", "AI Settings"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                OpenAISettingsDialog();
        }

        private void OpenAISettingsDialog()
        {
            var dialog = new AISettingsDialog
            {
                Owner = this,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
            };
            dialog.ShowDialog();
            RefreshApplyMode();
        }

        private static string BuildAIErrorMessage(Exception ex, string prefix = "Error")
        {
            var message = $"{prefix}: {ex.Message}";

            return message;
        }

        #endregion
    }
}
