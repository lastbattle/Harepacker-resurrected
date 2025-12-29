/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// WPF Window for AI-based map editing.
    /// One instance is created per map/board.
    /// </summary>
    public partial class AIMapEditWindow : Window
    {
        private static readonly Dictionary<Board, AIMapEditWindow> instances = new Dictionary<Board, AIMapEditWindow>();

        private readonly Board board;
        private bool isProcessing = false;
        private bool instructionsModified = false;

        private AIMapEditWindow(Board board)
        {
            this.board = board;

            InitializeComponent();

            // Reset flag after InitializeComponent (default text triggers TextChanged)
            instructionsModified = false;

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
                LogMessage($"Loaded map context ({text.Length} characters)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading map context: {ex.Message}");
                txtMapContext.Text = $"# Error loading map: {ex.Message}";
            }
        }

        #endregion

        #region Event Handlers

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadMapContext();
        }

        private async void BtnProcessAI_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing)
            {
                LogMessage("Already processing, please wait...");
                return;
            }

            if (!AISettings.IsConfigured)
            {
                var dialog = new AISettingsDialog();
                dialog.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                dialog.ShowDialog();

                // Check again after dialog closes
                if (!AISettings.IsConfigured)
                {
                    LogMessage("API key not configured. Please set up your OpenRouter API key in Settings.");
                    return;
                }
            }

            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "Process with AI", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var instructions = txtInstructions.Text;
            if (string.IsNullOrWhiteSpace(instructions) || !instructionsModified)
            {
                MessageBox.Show("Please enter your instructions in natural language.", "Process with AI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                isProcessing = true;
                btnProcessAI.IsEnabled = false;
                btnProcessAI.Content = "Processing...";

                // Get map context
                var serializer = new MapAISerializer(board);
                var mapContext = serializer.GenerateAISummary();

                // Use multi-agent orchestrator for layer-by-layer editing
                LogMessage("Analyzing request...");
                var orchestrator = new AgentOrchestrator(AISettings.ApiKey, AISettings.Model, AISettings.Model);
                orchestrator.OnProgress += (msg) => Dispatcher.Invoke(() => LogMessage(msg));

                string result = await orchestrator.ProcessWithAgentsAsync(mapContext, instructions);

                txtCommands.Text = result;
                LogMessage($"AI generated {result.Split('\n').Length} command(s)");
                LogMessage("Review the commands and click 'Execute Commands' to apply them.");
            }
            catch (Exception ex)
            {
                LogMessage($"AI processing error: {ex.Message}");
                MessageBox.Show($"Error processing with AI: {ex.Message}", "AI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isProcessing = false;
                btnProcessAI.IsEnabled = true;
                btnProcessAI.Content = "Process with AI";
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "Execute Commands", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var commandText = txtCommands.Text;
            if (string.IsNullOrWhiteSpace(commandText) ||
                commandText.StartsWith("# Commands will appear"))
            {
                MessageBox.Show("No commands to execute. Use 'Process with AI' first or enter commands manually.",
                    "Execute Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var parser = new MapAIParser();
                var commands = parser.ParseCommands(commandText);

                if (commands.Count == 0)
                {
                    LogMessage("No valid commands found in input");
                    return;
                }

                LogMessage($"Parsed {commands.Count} command(s)");

                var executor = new MapAIExecutor(board);
                var result = executor.ExecuteCommands(commands);

                // Log execution results
                foreach (var logEntry in result.Log)
                {
                    LogMessage(logEntry);
                }

                LogMessage($"Execution complete: {result.SuccessCount} succeeded, {result.FailCount} failed");

                if (result.SuccessCount > 0)
                {
                    board.Dirty = true;

                    // Auto-refresh map context to show updated state
                    LoadMapContext();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error executing commands: {ex.Message}");
                MessageBox.Show($"Error executing commands: {ex.Message}", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AISettingsDialog();
            dialog.ShowDialog();
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private async void BtnRunTests_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing)
            {
                LogMessage("Already processing, please wait...");
                return;
            }

            if (!AISettings.IsConfigured)
            {
                var dialog = new AISettingsDialog();
                dialog.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                dialog.ShowDialog();

                if (!AISettings.IsConfigured)
                {
                    LogMessage("API key not configured. Please set up your OpenRouter API key in Settings.");
                    return;
                }
            }

            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "Run Tests", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                isProcessing = true;
                btnRunTests.IsEnabled = false;
                btnProcessAI.IsEnabled = false;
                btnRunTests.Content = "Running...";

                // Load test prompt
                var testPrompt = MapEditorPromptBuilder.LoadPromptFile("ComprehensiveTestPrompt.txt");
                txtInstructions.Text = testPrompt;
                LogMessage("=== RUNNING AUTOMATED TESTS ===");

                // Get map context
                var serializer = new MapAISerializer(board);
                var mapContext = serializer.GenerateAISummary();

                // Run AI processing
                LogMessage("Processing test prompt with AI...");
                var orchestrator = new AgentOrchestrator(AISettings.ApiKey, AISettings.Model, AISettings.Model);
                orchestrator.OnProgress += (msg) => Dispatcher.Invoke(() => LogMessage(msg));

                string result = await orchestrator.ProcessWithAgentsAsync(mapContext, testPrompt);
                txtCommands.Text = result;

                // Calculate and display test score
                var testResults = CalculateTestScore(result);
                DisplayTestResults(testResults);
            }
            catch (Exception ex)
            {
                LogMessage($"Test error: {ex.Message}");
                MessageBox.Show($"Error running tests: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isProcessing = false;
                btnRunTests.IsEnabled = true;
                btnProcessAI.IsEnabled = true;
                btnRunTests.Content = "Run Tests";
            }
        }

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
            LogMessage("");
            LogMessage("=== TEST RESULTS ===");
            LogMessage("");

            int passed = 0;
            int total = 0;

            // Query-first tests
            LogMessage("QUERY-FIRST WORKFLOW:");
            passed += LogTest("  get_mob_list called", results.QueryMobList, ref total);
            passed += LogTest("  get_npc_list called", results.QueryNpcList, ref total);
            passed += LogTest("  get_object_info called", results.QueryObjectInfo, ref total);
            passed += LogTest("  get_background_info called", results.QueryBackgroundInfo, ref total);
            passed += LogTest("  get_bgm_list called", results.QueryBgmList, ref total);
            passed += LogTest("  No query violations", results.QueryViolations == 0, ref total);

            LogMessage("");
            LogMessage("STRUCTURE:");
            passed += LogTest("  Platforms created", results.PlatformsCreated >= 3, ref total);
            passed += LogTest("  Tiles created", results.TilesCreated >= 1, ref total);
            passed += LogTest("  Walls created", results.WallsCreated >= 2, ref total);
            passed += LogTest("  Ropes created", results.RopesCreated >= 1, ref total);
            passed += LogTest("  Ladders created", results.LaddersCreated >= 1, ref total);

            LogMessage("");
            LogMessage("LIFE:");
            passed += LogTest("  Mobs created with patrol ranges", results.MobsWithPatrol >= 5, ref total);
            passed += LogTest("  No mobs without patrol ranges", results.MobsWithoutPatrol == 0, ref total);
            passed += LogTest("  NPCs created", results.NpcsCreated >= 2, ref total);

            LogMessage("");
            LogMessage("DECORATION:");
            passed += LogTest("  Objects created", results.ObjectsCreated >= 3, ref total);
            passed += LogTest("  Backgrounds created", results.BackgroundsCreated >= 2, ref total);

            LogMessage("");
            LogMessage("PORTALS:");
            passed += LogTest("  Portals created", results.PortalsCreated >= 3, ref total);

            LogMessage("");
            LogMessage("MAP SETTINGS:");
            passed += LogTest("  Map size set", results.MapSizeSet, ref total);
            passed += LogTest("  VR (camera bounds) set", results.VRSet, ref total);
            passed += LogTest("  Snow weather enabled", results.SnowEnabled, ref total);
            passed += LogTest("  Town flag set", results.TownSet, ref total);
            passed += LogTest("  Mob rate set", results.MobRateSet, ref total);
            passed += LogTest("  Return map set", results.ReturnMapSet, ref total);
            passed += LogTest("  Level limit set", results.LevelLimitSet, ref total);
            passed += LogTest("  Field limit set", results.FieldLimitSet, ref total);
            passed += LogTest("  Map description set", results.MapDescSet, ref total);
            passed += LogTest("  Help text set", results.HelpSet, ref total);
            passed += LogTest("  Tooltip added", results.TooltipAdded, ref total);
            passed += LogTest("  BGM set", results.BgmSet, ref total);

            // Calculate percentage
            double percentage = total > 0 ? (double)passed / total * 100 : 0;
            string grade = percentage >= 95 ? "A+" :
                          percentage >= 90 ? "A" :
                          percentage >= 85 ? "B+" :
                          percentage >= 80 ? "B" :
                          percentage >= 70 ? "C" :
                          percentage >= 60 ? "D" : "F";

            LogMessage("");
            LogMessage("===========================================");
            LogMessage($"  SCORE: {passed}/{total} ({percentage:F1}%) - Grade: {grade}");
            LogMessage("===========================================");

            // Show message box with summary
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

        private int LogTest(string testName, bool passed, ref int total)
        {
            total++;
            string status = passed ? "[PASS]" : "[FAIL]";
            LogMessage($"{status} {testName}");
            return passed ? 1 : 0;
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

        private void TxtInstructions_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            instructionsModified = true;
        }

        #endregion

        #region Logging

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");

            // Scroll after layout is updated
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                txtLog.ScrollToEnd();
            }));
        }

        #endregion
    }
}
