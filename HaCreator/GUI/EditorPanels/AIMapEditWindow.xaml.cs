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
using System.Windows.Controls;

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
            mentionTimer.Tick += (_, _) => UpdateMentionSuggestions();
            EditorPanelLocalizer.Attach(this);
            RefreshSelectedModel();
            Activated += (_, _) => RefreshSelectedModel();
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
            mentionTimer.Stop();
            mentionPopup.IsOpen = false;
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
                window.mentionTimer.Stop();
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
                window.mentionTimer.Stop();
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
            mentionCatalog = null;
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
            if (mentionTimer.IsEnabled && (e.Key == Key.Enter || e.Key == Key.Tab)) UpdateMentionSuggestions();
            if (mentionPopup.IsOpen)
            {
                if (e.Key == Key.Escape) { mentionTimer.Stop(); mentionPopup.IsOpen = false; e.Handled = true; return; }
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    int count = mentionSuggestions.Items.Count;
                    if (count > 0)
                    {
                        mentionSuggestions.SelectedIndex = (mentionSuggestions.SelectedIndex + (e.Key == Key.Down ? 1 : count - 1)) % count;
                        mentionSuggestions.ScrollIntoView(mentionSuggestions.SelectedItem);
                    }
                    e.Handled = true; return;
                }
                if ((e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Right) && Keyboard.Modifiers == ModifierKeys.None)
                { AcceptMention(e.Key == Key.Right); e.Handled = true; return; }
            }
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
            TxtMessageInput_SelectionChanged(sender, e);
        }

        private WzMentionCatalog mentionCatalog;
        private bool updatingMention;
        private int mentionStart;
        private readonly System.Windows.Threading.DispatcherTimer mentionTimer = new()
        { Interval = TimeSpan.FromMilliseconds(180) };

        private void TxtMessageInput_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (updatingMention || mentionPopup == null) return;
            mentionTimer.Stop();
            mentionTimer.Start();
        }

        private void UpdateMentionSuggestions()
        {
            if (updatingMention || mentionPopup == null) return;
            mentionTimer.Stop();
            int caret = txtMessageInput.CaretIndex;
            string before = txtMessageInput.Text.Substring(0, Math.Min(caret, txtMessageInput.Text.Length));
            int at = before.LastIndexOf('@');
            if (at < 0 || (at > 0 && !char.IsWhiteSpace(before[at - 1])) || txtMessageInput.SelectionLength > 0)
            { mentionPopup.IsOpen = false; return; }
            string query = before.Substring(at + 1);
            if (query.IndexOfAny(new[] { '{', '}', '\r', '\n' }) >= 0)
            { mentionPopup.IsOpen = false; return; }
            mentionStart = at;
            try
            {
                mentionCatalog ??= new WzMentionCatalog();
                var matches = mentionCatalog.Search(query);
                mentionSuggestions.ItemsSource = matches;
                mentionSuggestions.SelectedIndex = matches.Count > 0 ? 0 : -1;
                mentionStatus.Text = matches.Count == 0 ? "No matches in loaded data." : "Enter/Tab: reference • Right: browse • Esc: dismiss • Up to 100 results";
                mentionPopup.IsOpen = true;
            }
            catch (Exception ex)
            {
                mentionSuggestions.ItemsSource = null;
                mentionStatus.Text = "Unable to browse WZ data: " + ex.Message;
                mentionPopup.IsOpen = true;
            }
        }

        private void AcceptMention(bool browse)
        {
            if (mentionSuggestions.SelectedItem is not WzMentionCatalog.Entry entry) return;
            if (browse && !entry.CanBrowse) return;
            string text = browse || entry.Path.EndsWith("/") ? "@" + entry.Path.TrimEnd('/') + "/" : "@{" + entry.Path + "} ";
            updatingMention = true;
            try
            {
                txtMessageInput.Select(mentionStart, txtMessageInput.CaretIndex - mentionStart);
                txtMessageInput.SelectedText = text;
                txtMessageInput.CaretIndex = mentionStart + text.Length;
                mentionPopup.IsOpen = false;
                txtMessageInput.Focus();
            }
            finally { updatingMention = false; }
            if (!text.EndsWith(" ")) UpdateMentionSuggestions();
        }

        private void MentionSuggestion_Click(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            var item = ItemsControl.ContainerFromElement(mentionSuggestions, element) as ListBoxItem;
            if (item == null) return;
            mentionSuggestions.SelectedItem = item.DataContext;
            AcceptMention(false);
            e.Handled = true;
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
                    if (!string.IsNullOrWhiteSpace(result.Command))
                    {
                        assistantMessage.AddEdit(result, applyChanges);
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
                RefreshApplyMode();
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

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing || !_chatSession.HasCommands || board == null) return;
            var selected = _chatSession.Messages.SelectMany(m => m.Edits).Where(e => e.IsPending && e.IsSelected).ToList();
            isProcessing = true;
            requestCancellation = new CancellationTokenSource();
            btnExecute.IsEnabled = false; btnSend.IsEnabled = false; btnStop.IsEnabled = true;
            btnClearChat.IsEnabled = false; chkLiveEdits.IsEnabled = false;
            int applied = 0;
            try
            {
                foreach (var edit in selected)
                {
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    if (requestCancellation.IsCancellationRequested) break;
                    if (!edit.TryBeginApply()) continue;
                    try
                    {
                        var result = ExecuteCommandText(edit.Command);
                        bool success = result != null && result.FailCount == 0;
                        edit.Complete(success, result == null ? "No valid commands." : string.Join("\n", result.Log));
                        if (!success) break;
                        applied++;
                    }
                    catch (Exception ex) { edit.Complete(false, ex.Message); break; }
                }
                int remaining = _chatSession.Messages.Sum(m => m.Edits.Count(e => e.IsPending));
                txtProgress.Text = $"Applied {applied} of {selected.Count} selected changes. " +
                    (remaining > 0 ? $"{remaining} changes remain ready for review." : applied == selected.Count ? "Review complete." : "Inspect failed changes in the review list.");
            }
            finally
            {
                isProcessing = false; requestCancellation.Dispose(); requestCancellation = null;
                btnSend.IsEnabled = !string.IsNullOrWhiteSpace(txtMessageInput.Text); btnStop.IsEnabled = false;
                btnClearChat.IsEnabled = true; chkLiveEdits.IsEnabled = true; RefreshApplyMode(); LoadMapContext();
            }
        }

        private void ReviewSelection_Changed(object sender, RoutedEventArgs e) => RefreshApplyMode();

        private void SelectReview_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing || sender is not System.Windows.Controls.Button button || button.DataContext is not ChatMessage message) return;
            bool select = (string)button.Tag == "all";
            foreach (var edit in message.Edits) edit.IsSelected = select;
            RefreshApplyMode();
        }

        private void PasteReview_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing) return;
            var dialog = new Window { Owner = this, Title = "Paste changes for review", Width = 660, Height = 400,
                MinWidth = 460, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var layout = new System.Windows.Controls.DockPanel { Margin = new Thickness(16) };
            var help = new System.Windows.Controls.TextBlock { Text = "Paste edits copied with Copy compact. This creates a checklist; your map changes only when you apply it.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            System.Windows.Controls.DockPanel.SetDock(help, System.Windows.Controls.Dock.Top); layout.Children.Add(help);
            var import = new System.Windows.Controls.Button { Content = "Review changes", Height = 32, Margin = new Thickness(0, 12, 0, 0) };
            System.Windows.Controls.DockPanel.SetDock(import, System.Windows.Controls.Dock.Bottom); layout.Children.Add(import);
            var input = new System.Windows.Controls.TextBox { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
            layout.Children.Add(input); dialog.Content = layout;
            import.Click += (_, _) =>
            {
                try { _chatSession.ImportCompactEdits(input.Text); RefreshApplyMode(); dialog.Close(); }
                catch (ArgumentException ex) { MessageBox.Show(dialog, ex.Message, "Cannot import changes", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            dialog.ShowDialog();
        }

        private void CopyReview_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.DataContext is not ChatMessage message) return;
            var rows = message.Edits.Where(edit => edit.IsSelected).ToList();
            var text = (string)button.Tag == "compact" && rows.All(edit => edit.CompactCode != null)
                ? CompactMapEdits.Encode(rows.SelectMany(edit => CompactMapEdits.Decode(edit.CompactCode)))
                : string.Join(Environment.NewLine, rows.Select(edit => edit.Command));
            if (text.Length == 0) { txtProgress.Text = "Select changes to copy."; return; }
            try { Clipboard.SetText(text); txtProgress.Text = "Selected changes copied."; }
            catch (System.Runtime.InteropServices.ExternalException) { txtProgress.Text = "Clipboard is busy. Try copying again."; }
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
            if (btnExecute == null) return;
            btnExecute.Visibility = _chatSession.Messages.Any(m => m.Edits.Any(e => e.IsPending)) ? Visibility.Visible : Visibility.Collapsed;
            btnExecute.IsEnabled = !isProcessing && _chatSession.HasCommands;
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
            RefreshSelectedModel();
            RefreshApplyMode();
        }

        private void RefreshSelectedModel()
        {
            txtSelectedModel.Text = AISettings.Model;
        }

        private static string BuildAIErrorMessage(Exception ex, string prefix = "Error")
        {
            var message = $"{prefix}: {ex.Message}";

            return message;
        }

        #endregion
    }
}
