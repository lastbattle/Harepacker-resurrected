/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows;
using System.Windows.Controls;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// WPF UserControl for AI-based map layout editing.
    /// Allows exporting map to text format for AI understanding and
    /// executing AI-generated commands to modify the map.
    /// </summary>
    public partial class AIMapEditPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private Board directBoard;  // For popup mode - specific board instance
        private bool isProcessing = false;

        public AIMapEditPanel()
        {
            InitializeComponent();
        }

        public void SetHaCreatorStateManager(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
        }

        /// <summary>
        /// Set a specific board for popup mode (one form per map)
        /// </summary>
        public void SetBoard(Board board)
        {
            this.directBoard = board;
        }

        private Board GetCurrentBoard()
        {
            // If a direct board is set (popup mode), use it
            if (directBoard != null)
                return directBoard;

            // Otherwise use the selected board from hcsm (embedded mode)
            if (hcsm?.MultiBoard?.SelectedBoard != null)
                return hcsm.MultiBoard.SelectedBoard;

            return null;
        }

        /// <summary>
        /// Loads the current map context into the description panel.
        /// Called automatically when the form is shown.
        /// </summary>
        public void LoadMapContext()
        {
            var board = GetCurrentBoard();
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

            var board = GetCurrentBoard();
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
            var board = GetCurrentBoard();
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
                    // Mark the board as dirty to indicate changes
                    board.Dirty = true;
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

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            txtLog.ScrollToEnd();
        }
    }
}
