using HaCreator.GUI.EditorPanels;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Input;
using MapleLib.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    /// <summary>
    /// Interaction logic for HaEditor2.xaml
    /// </summary>
    public partial class HaEditor : Window
    {
        private InputHandler handler;
        public HaCreatorStateManager hcsm;
        private bool _isObjectViewerInitialized;
        private bool _isMapExplorerInitialized;
        private bool _isMapExplorerHistoryInitialized;
        private string _mapLoadErrorsLogFilePath;

        public HaEditor()
        {
            InitializeComponent();

            Program.HaEditorWindow = this;

            this.Loaded += HaEditor2_Loaded;
            this.Closed += HaEditor2_Closed;
            this.StateChanged += HaEditor2_StateChanged;
        }

        /// <summary>
        /// On window state changed
        /// Normal, Minimized, Maximized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaEditor2_StateChanged(object sender, EventArgs e)
        {
            multiBoard.UpdateWindowState(this.WindowState);
        }

        /// <summary>
        /// Window size change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            multiBoard.UpdateWindowSize(e.NewSize);

            int newHeight = (int)(e.NewSize.Height * 0.75);

            tilePanelHost.Height = newHeight;
            objPanelHost.Height = newHeight;
            lifePanelHost.Height = newHeight;
            portalPanelHost.Height = newHeight;
            bgPanelHost.Height = newHeight;
            bgBlackBorderPanelHost.Height = Math.Min(250, newHeight);
            commonPanelHost.Height = newHeight;

        }

        private void HaEditor2_Loaded(object sender, RoutedEventArgs e)
        {
            // helper classes
            handler = new InputHandler(multiBoard);
            hcsm = new HaCreatorStateManager(
                multiBoard, ribbon, tabControl1, handler, editorPanel,
                textblock_CursorX, textblock_CursorY, textblock_RCursorX, textblock_RCursorY, textblock_selectedItem);
            hcsm.CloseRequested += Hcsm_CloseRequested;
            hcsm.FirstMapLoaded += Hcsm_FirstMapLoaded;

            tilePanel.Initialize(hcsm);
            objPanel.Initialize(hcsm);
            lifePanel.Initialize(hcsm);
            portalPanel.Initialize(hcsm);
            bgPanel.Initialize(hcsm);
            bgBlackBorderPanel.Initialize(hcsm);
            commonPanel.Initialize(hcsm);
            InitializeMapExplorer();

            // Initialize hot swap after all panels are registered
            hcsm.InitializeHotSwap();

            if (!hcsm.backupMan.AttemptRestore())
            {
                ShowMapExplorer();
            }
        }

        /// <summary>
        /// On tab selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (multiBoard.SelectedBoard == null)
                return;

            UpdateZoomLabel();
        }

        /// <summary>
        /// Mouse wheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var el = (UIElement)sender;

            if (multiBoard.TriggerMouseWheel(e, el))
            {
                base.OnMouseWheel(e);
            }
        }

        void Hcsm_CloseRequested()
        {
            Close();
        }

        void Hcsm_FirstMapLoaded()
        {
            // Auto-show Object Viewer if setting is enabled
            if (ApplicationSettings.ShowObjectViewerOnLoad)
            {
                // Use Dispatcher to ensure window is fully loaded before showing Object Viewer
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowObjectViewerDocked(isManualOpen: false);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// On window closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaEditor2_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!Program.Restarting && System.Windows.MessageBox.Show("Are you sure you want to quit?", "Quit", MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
            else
            {
                // Thread safe without locks since reference assignment is atomic
                Program.AbortThreads = true;
            }
        }

        /// <summary>
        /// On form close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaEditor2_Closed(object sender, EventArgs e)
        {
            // Close all AI Map Editor popups
            AIMapEditWindow.CloseAll();

            // Close any fallback floating tool windows
            ObjectViewerWindow.ForceClose();
            MapLoadErrorsWindow.ForceClose();

            multiBoard.Stop();
        }

        /// <summary>
        /// Handles tool-window tab focus changes.
        /// </summary>
        private void ToolWindowsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || hcsm == null)
            {
                return;
            }

            if (Equals(toolWindowsTabControl.SelectedItem, mapExplorerTabItem))
            {
                InitializeMapExplorer();
            }

            if (Equals(toolWindowsTabControl.SelectedItem, objectViewerTabItem))
            {
                ShowObjectViewerDocked(isManualOpen: false);
            }
        }

        private void InitializeMapExplorer()
        {
            if (_isMapExplorerInitialized)
            {
                return;
            }

            mapExplorerBrowser.InitializeMapsListboxItem(true);
            mapExplorerBrowser.SelectionChanged += MapExplorerBrowser_SelectionChanged;
            mapExplorerHamPathTextBox.Text = ApplicationSettings.LastHamPath ?? string.Empty;
            mapExplorerXmlPathTextBox.Text = ApplicationSettings.LastXmlPath ?? string.Empty;
            _isMapExplorerInitialized = true;
            InitializeMapExplorerHistory();
            UpdateMapExplorerSelectionState();
        }

        private void InitializeMapExplorerHistory()
        {
            if (_isMapExplorerHistoryInitialized)
            {
                return;
            }

            mapExplorerHistoryBrowser.IsHistoryMapBrowser = true;
            mapExplorerHistoryBrowser.PreviewPanelVisible = true;
            mapExplorerHistoryBrowser.InitialiseHistoryListboxItem();
            mapExplorerHistoryBrowser.SelectionChanged += MapExplorerHistoryBrowser_SelectionChanged;
            _isMapExplorerHistoryInitialized = true;
            UpdateMapExplorerSelectionState();
        }

        public void ShowMapExplorer(string mapNameFilter = null)
        {
            InitializeMapExplorer();
            toolWindowsTabControl.SelectedItem = mapExplorerTabItem;
            mapExplorerSourceTabControl.SelectedItem = mapExplorerWzTabItem;

            if (mapNameFilter != null)
            {
                mapExplorerSearchTextBox.Focus();
                mapExplorerSearchTextBox.Text = mapNameFilter;
                mapExplorerSearchTextBox.SelectAll();
            }
        }

        private void MapExplorerBrowser_SelectionChanged()
        {
            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerHistoryBrowser_SelectionChanged()
        {
            UpdateMapExplorerSelectionState();
        }

        private void UpdateMapExplorerSelectionState()
        {
            if (!_isMapExplorerInitialized)
            {
                mapExplorerLoadButton.IsEnabled = false;
                mapExplorerCheckMapButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.IsEnabled = false;
                mapExplorerSelectionTextBlock.Text = string.Empty;
                return;
            }

            if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerHistoryTabItem))
            {
                string selectedItem = _isMapExplorerHistoryInitialized ? mapExplorerHistoryBrowser.SelectedItem : null;
                mapExplorerResolveMissingButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.IsEnabled = false;
                mapExplorerLoadButton.IsEnabled = _isMapExplorerHistoryInitialized && mapExplorerHistoryBrowser.LoadMapEnabled;
                mapExplorerDeleteHistoryButton.IsEnabled = !string.IsNullOrEmpty(selectedItem);
                mapExplorerReloadButton.IsEnabled = true;
                mapExplorerSelectionTextBlock.Text = selectedItem ??
                    (_isMapExplorerHistoryInitialized ? $"History: {mapExplorerHistoryBrowser.ItemCount} map(s)" : string.Empty);
                return;
            }

            mapExplorerDeleteHistoryButton.IsEnabled = false;
            if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerHamTabItem))
            {
                mapExplorerResolveMissingButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.IsEnabled = false;
                mapExplorerLoadButton.IsEnabled = File.Exists(mapExplorerHamPathTextBox.Text);
                mapExplorerReloadButton.IsEnabled = false;
                mapExplorerSelectionTextBlock.Text = mapExplorerHamPathTextBox.Text;
                return;
            }

            if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerXmlTabItem))
            {
                mapExplorerResolveMissingButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.Visibility = Visibility.Collapsed;
                mapExplorerCheckMapButton.IsEnabled = false;
                mapExplorerLoadButton.IsEnabled = File.Exists(mapExplorerXmlPathTextBox.Text);
                mapExplorerReloadButton.IsEnabled = false;
                mapExplorerSelectionTextBlock.Text = mapExplorerXmlPathTextBox.Text;
                return;
            }

            string wzSelectedItem = mapExplorerBrowser.SelectedItem;
            mapExplorerResolveMissingButton.Visibility = Visibility.Visible;
            mapExplorerCheckMapButton.Visibility = Visibility.Visible;
            mapExplorerCheckMapButton.IsEnabled = Program.InfoManager != null &&
                (Program.DataSource != null || Program.WzManager != null);
            mapExplorerLoadButton.IsEnabled = mapExplorerBrowser.LoadMapEnabled;
            mapExplorerReloadButton.IsEnabled = true;
            mapExplorerSelectionTextBlock.Text = wzSelectedItem ?? string.Empty;
        }

        private void MapExplorerSourceTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, mapExplorerSourceTabControl))
            {
                return;
            }

            if (!IsLoaded || !_isMapExplorerInitialized)
            {
                return;
            }

            if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerHistoryTabItem))
            {
                InitializeMapExplorerHistory();
                mapExplorerHistoryBrowser.ReloadHistoryListboxItem();
            }

            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isMapExplorerInitialized)
            {
                return;
            }

            mapExplorerBrowser.ApplySearch(mapExplorerSearchTextBox.Text);
        }

        private void MapExplorerTownOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isMapExplorerInitialized)
            {
                return;
            }

            mapExplorerBrowser.TownOnlyFilter = mapExplorerTownOnlyCheckBox.IsChecked == true;
            mapExplorerBrowser.ApplySearch(mapExplorerSearchTextBox.Text);
        }

        private void MapExplorerReloadButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeMapExplorer();

            if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerHistoryTabItem))
            {
                InitializeMapExplorerHistory();
                mapExplorerHistoryBrowser.ReloadHistoryListboxItem();
            }
            else
            {
                mapExplorerBrowser.ReloadMapsListboxItem(true);
                mapExplorerBrowser.ApplySearch(mapExplorerSearchTextBox.Text);
            }

            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerCheckMapButton_Click(object sender, RoutedEventArgs e)
        {
            WaitWindow waitWindow = new WaitWindow("Checking maps...");
            waitWindow.Show();
            Forms.Application.DoEvents();

            try
            {
                MapCheckService.CheckLoadedMaps();
                System.Windows.MessageBox.Show(
                    "Check for map errors completed. See 'Errors_MapDebug.txt' for more information.",
                    "Check map",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Unable to check maps.\r\n\r\n{exception.Message}",
                    "Check map",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                waitWindow.EndWait();
            }
        }

        private void MapExplorerLoadButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeMapExplorer();

            if (hcsm == null)
            {
                return;
            }

            WaitWindow waitWindow = new WaitWindow("Loading...");
            waitWindow.Show();
            Forms.Application.DoEvents();

            try
            {
                string errorMessage = null;
                bool loaded;

                if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerHamTabItem))
                {
                    loaded = hcsm.LoadHamMap(mapExplorerHamPathTextBox.Text, out errorMessage);
                }
                else if (Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerXmlTabItem))
                {
                    loaded = hcsm.LoadXmlMap(mapExplorerXmlPathTextBox.Text, out errorMessage);
                }
                else
                {
                    bool fromHistory = Equals(mapExplorerSourceTabControl.SelectedItem, mapExplorerHistoryTabItem);
                    string selectedItem = fromHistory
                        ? mapExplorerHistoryBrowser.SelectedItem
                        : mapExplorerBrowser.SelectedItem;

                    if (string.IsNullOrEmpty(selectedItem))
                    {
                        return;
                    }

                    loaded = hcsm.LoadWzMapSelection(selectedItem, out errorMessage);
                    if (loaded && !fromHistory)
                    {
                        InitializeMapExplorerHistory();
                        mapExplorerHistoryBrowser.AddLoadedMapToHistory(selectedItem);
                    }
                }

                if (!loaded)
                {
                    System.Windows.MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                waitWindow.EndWait();
            }
        }

        private void MapExplorerBrowseHamButton_Click(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog dialog = new Forms.OpenFileDialog())
            {
                dialog.Title = "Select Map to load...";
                dialog.Filter = "HaCreator Map File (*.ham)|*.ham";
                if (!string.IsNullOrEmpty(mapExplorerHamPathTextBox.Text))
                {
                    dialog.FileName = mapExplorerHamPathTextBox.Text;
                }

                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                {
                    return;
                }

                mapExplorerHamPathTextBox.Text = dialog.FileName;
            }
        }

        private void MapExplorerBrowseXmlButton_Click(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog dialog = new Forms.OpenFileDialog())
            {
                dialog.Title = "Select XML to load...";
                dialog.Filter = "eXtensible Markup Language file (*.xml)|*.xml";
                if (!string.IsNullOrEmpty(mapExplorerXmlPathTextBox.Text))
                {
                    dialog.FileName = mapExplorerXmlPathTextBox.Text;
                }

                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                {
                    return;
                }

                mapExplorerXmlPathTextBox.Text = dialog.FileName;
            }
        }

        private void MapExplorerHamPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplicationSettings.LastHamPath = mapExplorerHamPathTextBox.Text;
            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerXmlPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplicationSettings.LastXmlPath = mapExplorerXmlPathTextBox.Text;
            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeMapExplorerHistory();
            mapExplorerHistoryBrowser.ClearLoadedMapHistory();
            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerDeleteHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeMapExplorerHistory();
            mapExplorerHistoryBrowser.RemoveSelectedMapFromHistory();
            UpdateMapExplorerSelectionState();
        }

        private void MapExplorerResolveMissingButton_Click(object sender, RoutedEventArgs e)
        {
            WaitWindow waitWindow = new WaitWindow("Resolving missing map strings...");
            waitWindow.Show();
            Forms.Application.DoEvents();

            try
            {
                MapLoadService.MissingMapResolutionResult result = MapLoadService.ResolveMissingMapStringEntries();

                mapExplorerBrowser.ReloadMapsListboxItem(true);
                mapExplorerBrowser.ApplySearch(mapExplorerSearchTextBox.Text);
                UpdateMapExplorerSelectionState();

                ShowScrollableMessage("Resolve Missing Maps", MapLoadService.BuildResolutionSummaryMessage(result));
            }
            catch (Exception ex)
            {
                ShowScrollableMessage("Resolve Missing Maps", $"Failed to resolve missing map strings.\r\n\r\n{ex}");
            }
            finally
            {
                waitWindow.EndWait();
            }
        }

        private void ShowScrollableMessage(string title, string message)
        {
            using (Forms.Form dialog = new Forms.Form())
            using (Forms.TextBox messageBox = new Forms.TextBox())
            using (Forms.Button okButton = new Forms.Button())
            {
                dialog.Text = title;
                dialog.StartPosition = Forms.FormStartPosition.CenterParent;
                dialog.Size = new System.Drawing.Size(720, 520);
                dialog.MinimumSize = new System.Drawing.Size(500, 320);
                dialog.FormBorderStyle = Forms.FormBorderStyle.SizableToolWindow;

                messageBox.Multiline = true;
                messageBox.ReadOnly = true;
                messageBox.ScrollBars = Forms.ScrollBars.Both;
                messageBox.WordWrap = false;
                messageBox.Dock = Forms.DockStyle.Fill;
                messageBox.Font = new System.Drawing.Font("Consolas", 9F);
                messageBox.Text = message;

                okButton.Text = "OK";
                okButton.Dock = Forms.DockStyle.Bottom;
                okButton.Height = 30;
                okButton.DialogResult = Forms.DialogResult.OK;

                dialog.Controls.Add(messageBox);
                dialog.Controls.Add(okButton);
                dialog.AcceptButton = okButton;

                dialog.ShowDialog();
            }
        }

        public void ShowObjectViewerDocked(bool isManualOpen = true)
        {
            if (multiBoard?.SelectedBoard == null)
            {
                if (isManualOpen)
                {
                    System.Windows.MessageBox.Show("No map is currently loaded.", "Object Viewer",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return;
            }

            if (isManualOpen)
            {
                ApplicationSettings.ShowObjectViewerOnLoad = true;
            }

            if (!_isObjectViewerInitialized)
            {
                objectViewerPanelDocked.Initialize(hcsm);
                _isObjectViewerInitialized = true;
            }

            objectViewerPanelDocked.OnBoardChanged(hcsm.MultiBoard.SelectedBoard);
            toolWindowsTabControl.SelectedItem = objectViewerTabItem;
        }

        public void ShowMapLoadErrors(
            string mapIdentifier,
            IReadOnlyDictionary<ErrorLevel, List<Error>> errorSnapshot,
            string logFilePath)
        {
            if (errorSnapshot == null || errorSnapshot.Count == 0)
            {
                return;
            }

            _mapLoadErrorsLogFilePath = logFilePath;
            mapLoadErrorsSummaryTextBlock.Text = MapLoadErrorsWindow.BuildSummaryText(mapIdentifier, errorSnapshot);
            mapLoadErrorsTextBox.Text = MapLoadErrorsWindow.BuildErrorText(errorSnapshot);
            mapLoadErrorsTextBox.ScrollToHome();
            mapLoadErrorsLogPathTextBlock.Text = string.IsNullOrEmpty(logFilePath)
                ? string.Empty
                : $"Saved to: {Path.GetFileName(logFilePath)}";
            mapLoadErrorsOpenLogButton.IsEnabled = !string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath);
            //toolWindowsTabControl.SelectedItem = mapLoadErrorsTabItem;
        }

        private void MapLoadErrorsClearButton_Click(object sender, RoutedEventArgs e)
        {
            _mapLoadErrorsLogFilePath = null;
            mapLoadErrorsSummaryTextBlock.Text = string.Empty;
            mapLoadErrorsTextBox.Text = string.Empty;
            mapLoadErrorsLogPathTextBlock.Text = string.Empty;
            mapLoadErrorsOpenLogButton.IsEnabled = false;
            toolWindowsTabControl.SelectedItem = toolboxTabItem;
        }

        private void MapLoadErrorsCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(mapLoadErrorsTextBox.Text))
            {
                Clipboard.SetText(mapLoadErrorsTextBox.Text);
            }
        }

        private void MapLoadErrorsOpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mapLoadErrorsLogFilePath) || !File.Exists(_mapLoadErrorsLogFilePath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _mapLoadErrorsLogFilePath,
                UseShellExecute = true
            });
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            Expander expanderSrc = sender as Expander;
            UIElement childContent = expanderSrc.Content as UIElement;

            childContent.Visibility = Visibility.Visible;

            if (expanderSrc == expander_bgBlackBorderPanel)
            {
                bgBlackBorderPanel.Initialize(hcsm); // re-initialize the panel to ensure it has the correct data
            }
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            Expander expanderSrc = sender as Expander;
            UIElement childContent = expanderSrc.Content as UIElement;

            childContent.Visibility = Visibility.Collapsed; // collapse when its not needed, speed up the performance here
        }

        /// <summary>
        /// Opens the AI Map Editor popup for the currently selected map
        /// </summary>
        private void ExpanderAIMapEditor_Expanded(object sender, RoutedEventArgs e)
        {
            // Collapse immediately - we use the expander as a styled button
            if (sender is Expander expander)
            {
                expander.IsExpanded = false;
            }

            var board = multiBoard?.SelectedBoard;
            if (board == null)
            {
                System.Windows.MessageBox.Show("No map is currently loaded.", "AI Map Editor",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AIMapEditWindow.ShowForBoard(board, this);
        }

        #region Zoom Controls
        /// <summary>
        /// Zoom in button click
        /// </summary>
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            var board = multiBoard?.SelectedBoard;
            if (board == null) return;

            board.ZoomIn();
            UpdateZoomLabel();
        }

        /// <summary>
        /// Zoom out button click
        /// </summary>
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            var board = multiBoard?.SelectedBoard;
            if (board == null) return;

            board.ZoomOut();
            UpdateZoomLabel();
        }

        /// <summary>
        /// Reset zoom to 100%
        /// </summary>
        private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
        {
            var board = multiBoard?.SelectedBoard;
            if (board == null) return;

            board.ResetZoom();
            UpdateZoomLabel();
        }

        /// <summary>
        /// Updates the zoom level display in the status bar
        /// </summary>
        public void UpdateZoomLabel()
        {
            var board = multiBoard?.SelectedBoard;
            if (board != null)
            {
                int zoomPercent = (int)(board.Zoom * 100);
                txtZoomLevel.Text = $"{zoomPercent}%";
            }
            else
            {
                txtZoomLevel.Text = "100%";
            }
        }
        #endregion
    }
}
