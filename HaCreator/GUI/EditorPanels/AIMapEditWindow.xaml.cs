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

        private AIMapEditWindow(Board board)
        {
            this.board = board;

            InitializeComponent();

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
                MessageBox.Show("Please configure your OpenRouter API key first.\nClick 'Settings' to set it up.",
                    "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (board == null)
            {
                MessageBox.Show("No map is currently loaded.", "Process with AI", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var instructions = txtInstructions.Text;
            if (string.IsNullOrWhiteSpace(instructions) ||
                instructions.StartsWith("Example instructions:") ||
                instructions == "Add 3 blue snails on the left side\nAdd a portal to Henesys on the right")
            {
                MessageBox.Show("Please enter your instructions in natural language.", "Process with AI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                isProcessing = true;
                btnProcessAI.IsEnabled = false;
                btnProcessAI.Content = "Processing...";
                LogMessage("Sending instructions to AI...");

                // Get map context
                var serializer = new MapAISerializer(board);
                var mapContext = serializer.GenerateAISummary();

                // Call OpenRouter
                var client = new OpenRouterClient(AISettings.ApiKey, AISettings.Model);
                var result = await client.ProcessInstructionsAsync(mapContext, instructions);

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

        #endregion

        #region Logging

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            txtLog.ScrollToEnd();
        }

        #endregion
    }
}
