using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;

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
        private bool isProcessing = false;

        private AIMapEditWindow(Board board)
        {
            this.board = board;

            // Initialize chat session
            _chatSession = new ChatSession();

            InitializeComponent();

            // Bind chat messages to ItemsControl
            chatItemsControl.ItemsSource = _chatSession.Messages;

            // Subscribe to collection changes for auto-scroll
            _chatSession.Messages.CollectionChanged += Messages_CollectionChanged;

            // Update title with map info
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string mapName = "Unknown";
            int mapId = 0;

            if (board?.MapInfo != null)
            {
                mapId = board.MapInfo.id;
                mapName = !string.IsNullOrEmpty(board.MapInfo.strMapName)
                    ? board.MapInfo.strMapName
                    : $"Map {mapId}";
            }

            this.Title = $"AI Map Editor - {mapName} ({mapId})";
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
                MessageBox.Show("No map is currently loaded.", "AI Map Editor",
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
                window.Close();
            }
        }

        /// <summary>
        /// Close all AI Map Edit windows
        /// </summary>
        public static void CloseAll()
        {
            foreach (var window in instances.Values)
            {
                window.Closing -= window.Window_Closing;
                window.Close();
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
                txtMapContext.Text = "# No map loaded";
                return;
            }

            try
            {
                var serializer = new MapAISerializer(board);
                var text = serializer.GenerateAISummary();
                txtMapContext.Text = text;

                // Update chat session with current map context
                _chatSession.CurrentMapContext = text;
            }
            catch (Exception ex)
            {
                txtMapContext.Text = $"# Error loading map: {ex.Message}";
            }
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

            // Check API configuration
            if (!AISettings.IsConfigured)
            {
                var dialog = new AISettingsDialog();
                dialog.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                dialog.ShowDialog();

                if (!AISettings.IsConfigured)
                {
                    return;
                }
            }

            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "AI Map Editor",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                isProcessing = true;
                btnSend.IsEnabled = false;
                btnExecute.IsEnabled = false;

                // Clear input
                txtMessageInput.Clear();

                // Add user message to chat
                _chatSession.AddUserMessage(userInput);

                // Add placeholder assistant message (shows "Thinking...")
                var assistantMessage = _chatSession.AddAssistantMessage();

                // Update map context
                var serializer = new MapAISerializer(board);
                _chatSession.CurrentMapContext = serializer.GenerateAISummary();

                // Process with AI using conversation history
                var orchestrator = new AgentOrchestrator(AISettings.ApiKey, AISettings.Model, AISettings.Model);

                // Use conversation-aware processing
                string result = await orchestrator.ProcessWithConversationAsync(
                    _chatSession.CurrentMapContext,
                    _chatSession.ToConversationHistory(),
                    userInput);

                // Parse response to separate explanation from commands
                var (explanation, commands) = ParseAIResponse(result);

                // Update assistant message
                assistantMessage.Content = explanation;
                assistantMessage.CommandsContent = commands;
                assistantMessage.IsProcessing = false;
            }
            catch (Exception ex)
            {
                if (_chatSession.LastAssistantMessage != null)
                {
                    _chatSession.LastAssistantMessage.IsProcessing = false;
                    _chatSession.LastAssistantMessage.HasError = true;
                    _chatSession.LastAssistantMessage.ErrorMessage = $"Error: {ex.Message}";
                }
            }
            finally
            {
                isProcessing = false;
                btnSend.IsEnabled = true;
                btnExecute.IsEnabled = _chatSession.HasCommands;
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
                "# QUERY:", "# WARNING:", "# "
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
            if (_chatSession.HasMessages)
            {
                var result = MessageBox.Show(
                    "Start a new conversation? Current chat history will be cleared.",
                    "New Chat",
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
            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "Execute Commands",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var commandText = _chatSession.GetLatestCommands();
            if (string.IsNullOrWhiteSpace(commandText))
            {
                MessageBox.Show("No commands to execute. Send a message to generate commands first.",
                    "Execute Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var parser = new MapAIParser();
                var commands = parser.ParseCommands(commandText);

                if (commands.Count == 0)
                {
                    MessageBox.Show("No valid commands found in the generated output.",
                        "Execute Commands", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var executor = new MapAIExecutor(board);
                var result = executor.ExecuteCommands(commands);

                // Show execution summary
                string summary = $"Execution complete: {result.SuccessCount} succeeded, {result.FailCount} failed";

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

                MessageBox.Show(summary, "Execution Result",
                    MessageBoxButton.OK,
                    result.FailCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                if (result.SuccessCount > 0)
                {
                    board.Dirty = true;
                    LoadMapContext(); // Refresh context
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing commands: {ex.Message}", "Execution Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AISettingsDialog();
            dialog.ShowDialog();
        }

        private async void BtnRunTests_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing)
            {
                return;
            }

            if (!AISettings.IsConfigured)
            {
                var dialog = new AISettingsDialog();
                dialog.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                dialog.ShowDialog();

                if (!AISettings.IsConfigured)
                {
                    return;
                }
            }

            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "Run Tests",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                isProcessing = true;
                btnRunTests.IsEnabled = false;
                btnSend.IsEnabled = false;

                // Load test prompt
                var testPrompt = MapEditorPromptBuilder.LoadPromptFile("ComprehensiveTestPrompt.txt");

                // Clear chat and add test prompt as user message
                _chatSession.Clear();
                _chatSession.AddUserMessage("=== RUNNING AUTOMATED TESTS ===\n\n" + testPrompt);

                // Add placeholder assistant message
                var assistantMessage = _chatSession.AddAssistantMessage();

                // Get map context
                var serializer = new MapAISerializer(board);
                _chatSession.CurrentMapContext = serializer.GenerateAISummary();

                // Run AI processing
                var orchestrator = new AgentOrchestrator(AISettings.ApiKey, AISettings.Model, AISettings.Model);
                string result = await orchestrator.ProcessWithConversationAsync(
                    _chatSession.CurrentMapContext,
                    _chatSession.ToConversationHistory(),
                    testPrompt);

                // Parse and update assistant message
                var (explanation, commands) = ParseAIResponse(result);
                assistantMessage.Content = explanation;
                assistantMessage.CommandsContent = commands;
                assistantMessage.IsProcessing = false;

                // Calculate and display test score
                var testResults = CalculateTestScore(commands);
                DisplayTestResults(testResults);
            }
            catch (Exception ex)
            {
                if (_chatSession.LastAssistantMessage != null)
                {
                    _chatSession.LastAssistantMessage.IsProcessing = false;
                    _chatSession.LastAssistantMessage.HasError = true;
                    _chatSession.LastAssistantMessage.ErrorMessage = $"Test error: {ex.Message}";
                }
            }
            finally
            {
                isProcessing = false;
                btnRunTests.IsEnabled = true;
                btnSend.IsEnabled = true;
                btnExecute.IsEnabled = _chatSession.HasCommands;
            }
        }

        #endregion

        #region Test Scoring

        private TestResults CalculateTestScore(string output)
        {
            var results = new TestResults();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Query functions
                if (line.Contains("# QUERY: get_mob_list")) results.QueryMobList = true;
                if (line.Contains("# QUERY: get_npc_list")) results.QueryNpcList = true;
                if (line.Contains("# QUERY: get_object_info")) results.QueryObjectInfo = true;
                if (line.Contains("# QUERY: get_background_info")) results.QueryBackgroundInfo = true;
                if (line.Contains("# QUERY: get_bgm_list")) results.QueryBgmList = true;

                // Warnings (query violations)
                if (line.Contains("# WARNING:")) results.QueryViolations++;

                // Platforms
                if (line.StartsWith("ADD PLATFORM")) results.PlatformsCreated++;

                // Tiles
                if (line.StartsWith("TILE STRUCTURE") || line.StartsWith("TILE PLATFORM")) results.TilesCreated++;

                // Mobs with patrol ranges
                if (line.StartsWith("ADD MOB") && line.Contains("rx0=") && line.Contains("rx1=")) results.MobsWithPatrol++;
                else if (line.StartsWith("ADD MOB")) results.MobsWithoutPatrol++;

                // NPCs
                if (line.StartsWith("ADD NPC")) results.NpcsCreated++;

                // Objects
                if (line.StartsWith("ADD OBJECT")) results.ObjectsCreated++;

                // Backgrounds
                if (line.StartsWith("ADD BACKGROUND")) results.BackgroundsCreated++;

                // Portals
                if (line.StartsWith("ADD PORTAL")) results.PortalsCreated++;

                // Walls, Ropes, Ladders
                if (line.StartsWith("ADD WALL")) results.WallsCreated++;
                if (line.StartsWith("ADD ROPE")) results.RopesCreated++;
                if (line.StartsWith("ADD LADDER")) results.LaddersCreated++;

                // Settings
                if (line.StartsWith("SET MAP_SIZE")) results.MapSizeSet = true;
                if (line.StartsWith("SET VR")) results.VRSet = true;
                if (line.Contains("SET MAP_OPTION") && line.Contains("snow")) results.SnowEnabled = true;
                if (line.Contains("SET MAP_OPTION") && line.Contains("town")) results.TownSet = true;
                if (line.StartsWith("SET MOB_RATE")) results.MobRateSet = true;
                if (line.StartsWith("SET RETURN_MAP")) results.ReturnMapSet = true;
                if (line.StartsWith("SET LEVEL_LIMIT")) results.LevelLimitSet = true;
                if (line.StartsWith("SET FIELD_LIMIT")) results.FieldLimitSet = true;
                if (line.StartsWith("SET MAP_DESC")) results.MapDescSet = true;
                if (line.StartsWith("SET HELP")) results.HelpSet = true;
                if (line.StartsWith("ADD TOOLTIP")) results.TooltipAdded = true;
                if (line.StartsWith("SET BGM")) results.BgmSet = true;
            }

            return results;
        }

        private void DisplayTestResults(TestResults results)
        {
            int passed = 0;
            int total = 0;

            // Count passed tests
            passed += (results.QueryMobList ? 1 : 0); total++;
            passed += (results.QueryNpcList ? 1 : 0); total++;
            passed += (results.QueryObjectInfo ? 1 : 0); total++;
            passed += (results.QueryBackgroundInfo ? 1 : 0); total++;
            passed += (results.QueryBgmList ? 1 : 0); total++;
            passed += (results.QueryViolations == 0 ? 1 : 0); total++;
            passed += (results.PlatformsCreated >= 3 ? 1 : 0); total++;
            passed += (results.TilesCreated >= 1 ? 1 : 0); total++;
            passed += (results.WallsCreated >= 2 ? 1 : 0); total++;
            passed += (results.RopesCreated >= 1 ? 1 : 0); total++;
            passed += (results.LaddersCreated >= 1 ? 1 : 0); total++;
            passed += (results.MobsWithPatrol >= 5 ? 1 : 0); total++;
            passed += (results.MobsWithoutPatrol == 0 ? 1 : 0); total++;
            passed += (results.NpcsCreated >= 2 ? 1 : 0); total++;
            passed += (results.ObjectsCreated >= 3 ? 1 : 0); total++;
            passed += (results.BackgroundsCreated >= 2 ? 1 : 0); total++;
            passed += (results.PortalsCreated >= 3 ? 1 : 0); total++;
            passed += (results.MapSizeSet ? 1 : 0); total++;
            passed += (results.VRSet ? 1 : 0); total++;
            passed += (results.SnowEnabled ? 1 : 0); total++;
            passed += (results.TownSet ? 1 : 0); total++;
            passed += (results.MobRateSet ? 1 : 0); total++;
            passed += (results.ReturnMapSet ? 1 : 0); total++;
            passed += (results.LevelLimitSet ? 1 : 0); total++;
            passed += (results.FieldLimitSet ? 1 : 0); total++;
            passed += (results.MapDescSet ? 1 : 0); total++;
            passed += (results.HelpSet ? 1 : 0); total++;
            passed += (results.TooltipAdded ? 1 : 0); total++;
            passed += (results.BgmSet ? 1 : 0); total++;

            double percentage = total > 0 ? (double)passed / total * 100 : 0;
            string grade = percentage >= 95 ? "A+" :
                          percentage >= 90 ? "A" :
                          percentage >= 85 ? "B+" :
                          percentage >= 80 ? "B" :
                          percentage >= 70 ? "C" :
                          percentage >= 60 ? "D" : "F";

            MessageBox.Show(
                $"Test Results: {passed}/{total} passed ({percentage:F1}%)\n\nGrade: {grade}\n\n" +
                $"Query Functions: {(results.QueryMobList && results.QueryNpcList && results.QueryObjectInfo && results.QueryBackgroundInfo && results.QueryBgmList ? "All called" : "Some missing")}\n" +
                $"Query Violations: {results.QueryViolations}\n" +
                $"Mobs with Patrol: {results.MobsWithPatrol}\n" +
                $"Settings Applied: {CountSettings(results)}/12",
                "Test Results",
                MessageBoxButton.OK,
                percentage >= 90 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private int CountSettings(TestResults results)
        {
            int count = 0;
            if (results.MapSizeSet) count++;
            if (results.VRSet) count++;
            if (results.SnowEnabled) count++;
            if (results.TownSet) count++;
            if (results.MobRateSet) count++;
            if (results.ReturnMapSet) count++;
            if (results.LevelLimitSet) count++;
            if (results.FieldLimitSet) count++;
            if (results.MapDescSet) count++;
            if (results.HelpSet) count++;
            if (results.TooltipAdded) count++;
            if (results.BgmSet) count++;
            return count;
        }

        private class TestResults
        {
            // Query functions called
            public bool QueryMobList { get; set; }
            public bool QueryNpcList { get; set; }
            public bool QueryObjectInfo { get; set; }
            public bool QueryBackgroundInfo { get; set; }
            public bool QueryBgmList { get; set; }
            public int QueryViolations { get; set; }

            // Structure
            public int PlatformsCreated { get; set; }
            public int TilesCreated { get; set; }
            public int WallsCreated { get; set; }
            public int RopesCreated { get; set; }
            public int LaddersCreated { get; set; }

            // Life
            public int MobsWithPatrol { get; set; }
            public int MobsWithoutPatrol { get; set; }
            public int NpcsCreated { get; set; }

            // Decoration
            public int ObjectsCreated { get; set; }
            public int BackgroundsCreated { get; set; }

            // Portals
            public int PortalsCreated { get; set; }

            // Settings
            public bool MapSizeSet { get; set; }
            public bool VRSet { get; set; }
            public bool SnowEnabled { get; set; }
            public bool TownSet { get; set; }
            public bool MobRateSet { get; set; }
            public bool ReturnMapSet { get; set; }
            public bool LevelLimitSet { get; set; }
            public bool FieldLimitSet { get; set; }
            public bool MapDescSet { get; set; }
            public bool HelpSet { get; set; }
            public bool TooltipAdded { get; set; }
            public bool BgmSet { get; set; }
        }

        #endregion
    }
}
