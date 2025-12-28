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

        private AIMapEditWindow(Board board)
        {
            this.board = board;

            InitializeComponent();

            // Set the board directly on the panel
            panel.SetBoard(board);

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
            window.panel.LoadMapContext();
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
                // Check if window is still valid
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
    }
}
