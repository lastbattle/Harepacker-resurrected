using HaCreator.Wz;
using HaCreator.GUI.Localization;
using MapleLib.Img;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI
{
    /// <summary>
    /// Source-version map picker and explicit dependency review for cross-version imports.
    /// </summary>
    public partial class MapImportWindow : Window
    {
        private readonly ObservableCollection<SourceMapItem> _allMaps = new();
        private readonly ObservableCollection<MapImportAsset> _planAssets = new();
        private readonly List<MapImportPlan> _plans = new();
        private IDataSource _source;
        private bool _reviewed;
        private bool _busy;
        private bool _allowClose;
        private readonly bool _destinationWritable;

        public MapImportWindow()
        {
            InitializeComponent();
            _destinationWritable = Program.DataSource is ImgFileSystemDataSource ||
                (Program.DataSource is HybridDataSource hybrid && hybrid.ImgSource != null);
            mapsListView.ItemsSource = _allMaps;
            planDataGrid.ItemsSource = _planAssets;
            string destination = Program.DataSource?.VersionInfo?.DirectoryPath ?? Program.DataSource?.Name;
            destinationTextBlock.Text = string.IsNullOrWhiteSpace(destination)
                ? DialogTextExtension.Get("MapImport_DestinationCurrent")
                : DialogTextExtension.Format("MapImport_Destination", destination);
            sourcePathTextBox.Text = ApplicationSettings.LastMapImportSourcePath ?? string.Empty;
            if (!_destinationWritable)
            {
                // The importer writes extracted IMG files. Keep the dialog safe to
                // open from legacy WZ mode, but make its unsupported destination
                // explicit instead of allowing a review that can never execute.
                statusTextBlock.Text = DialogTextExtension.Get("MapImport_WritableDestination");
                sourcePathTextBox.IsEnabled = false;
                browseButton.IsEnabled = false;
                reviewButton.IsEnabled = false;
                importButton.IsEnabled = false;
            }
            Loaded += async (_, _) =>
            {
                if (_destinationWritable && Directory.Exists(sourcePathTextBox.Text))
                    await LoadSourceAsync(sourcePathTextBox.Text);
            };
        }

        private async void SourcePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplicationSettings.LastMapImportSourcePath = sourcePathTextBox.Text;
            if (_destinationWritable && IsLoaded && Directory.Exists(sourcePathTextBox.Text))
                await LoadSourceAsync(sourcePathTextBox.Text);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = DialogTextExtension.Get("MapImport_SelectSourceFolder"),
                SelectedPath = Directory.Exists(sourcePathTextBox.Text) ? sourcePathTextBox.Text : string.Empty,
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    sourcePathTextBox.Text = dialog.SelectedPath;
            }
        }

        private async Task LoadSourceAsync(string path)
        {
            if (!_destinationWritable || _busy || string.Equals(_source?.VersionInfo?.DirectoryPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                return;

            _busy = true;
            SetBusy(true, DialogTextExtension.Get("MapImport_Scanning"));
            try
            {
                string sourcePath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                string destinationPath = Program.DataSource?.VersionInfo?.DirectoryPath;
                if (!string.IsNullOrWhiteSpace(destinationPath) &&
                    string.Equals(sourcePath, Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(DialogTextExtension.Get("MapImport_SameSource"));
                _source?.Dispose();
                _source = await Task.Run(() => MapImportService.OpenSource(sourcePath));
                IReadOnlyDictionary<string, MapImportMapLabel> labels =
                    await Task.Run(() => MapImportService.GetMapLabels(_source));
                List<SourceMapItem> maps = await Task.Run(() => EnumerateMaps(path, labels, Program.DataSource));
                _allMaps.Clear();
                foreach (SourceMapItem map in maps)
                    _allMaps.Add(map);
                ApplyMapFilter();
                statusTextBlock.Text = DialogTextExtension.Format("MapImport_FoundMaps", _allMaps.Count);
            }
            catch (Exception ex)
            {
                _source?.Dispose();
                _source = null;
                _allMaps.Clear();
                statusTextBlock.Text = DialogTextExtension.Format("MapImport_OpenFailed", ex.Message);
            }
            finally
            {
                _busy = false;
                SetBusy(false, statusTextBlock.Text);
            }
        }

        private static List<SourceMapItem> EnumerateMaps(
            string root,
            IReadOnlyDictionary<string, MapImportMapLabel> labels,
            IDataSource destination)
        {
            string mapRoot = Path.Combine(root, "Map");
            if (!Directory.Exists(mapRoot))
                return new List<SourceMapItem>();

            // A version directory can contain editor backup snapshots. They are not
            // part of the source export and may duplicate map IDs, so do not descend
            // into reserved Backups folders while building the picker list.
            return HaCreatorPaths.EnumerateFilesExcludingBackups(mapRoot, "*.img", SearchOption.AllDirectories)
                .Where(path => Path.GetFileNameWithoutExtension(path).Length == 9 &&
                               Path.GetFileNameWithoutExtension(path).All(char.IsDigit))
                .Select(path =>
                {
                    string mapId = Path.GetFileNameWithoutExtension(path);
                    labels.TryGetValue(mapId, out MapImportMapLabel label);
                    return new SourceMapItem(
                        mapId,
                        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                        label?.StreetName,
                        label?.MapName,
                        destination?.ImageExists("Map", $"Map/Map{mapId[0]}/{mapId}.img") == true);
                })
                .GroupBy(item => item.MapId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.MapId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void MapFilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyMapFilter();

        private void OnlyMissingMapsCheckBox_Changed(object sender, RoutedEventArgs e) => ApplyMapFilter();

        private void ApplyMapFilter()
        {
            if (mapsListView == null)
                return;
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(mapsListView.ItemsSource);
            string filter = mapFilterTextBox?.Text?.Trim() ?? string.Empty;
            bool onlyMissingMaps = onlyMissingMapsCheckBox?.IsChecked == true;
            view.Filter = value => value is SourceMapItem item &&
                (!onlyMissingMaps || !item.IsAlreadyInDestination) &&
                (string.IsNullOrEmpty(filter) || item.SearchText.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        private void MapsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            mapCountTextBlock.Text = mapsListView.SelectedItems.Count == 0
                ? DialogTextExtension.Get("MapImport_NoMapsSelected")
                : DialogTextExtension.Format("MapImport_MapsSelected", mapsListView.SelectedItems.Count);
            reviewButton.IsEnabled = !_busy && _source != null && mapsListView.SelectedItems.Count > 0;
            _reviewed = false;
            importButton.IsEnabled = false;
        }

        private async void ReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_source == null || mapsListView.SelectedItems.Count == 0)
                return;

            _busy = true;
            SetBusy(true, DialogTextExtension.Get("MapImport_Analyzing"));
            try
            {
                _plans.Clear();
                foreach (SourceMapItem item in mapsListView.SelectedItems.Cast<SourceMapItem>())
                {
                    MapImportPlan plan = await Task.Run(() => new MapImportService(Program.DataSource, Program.InfoManager).Analyze(_source, item.MapId));
                    _plans.Add(plan);
                }

                _planAssets.Clear();
                foreach (MapImportAsset asset in _plans.SelectMany(plan => plan.Assets)
                    .GroupBy(asset => $"{asset.Kind}|{asset.Category}|{asset.RelativePath}|{asset.EntryPath}", StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(asset => asset.Kind).ThenBy(asset => asset.DisplayPath, StringComparer.OrdinalIgnoreCase))
                    _planAssets.Add(asset);

                UpdateMapReplacementChoice();
                _reviewed = true;
                importButton.IsEnabled = _plans.Count > 0;
                planSummaryTextBlock.Text = BuildPlanSummary();
                statusTextBlock.Text = DialogTextExtension.Get("MapImport_ReviewStatus");
            }
            catch (Exception ex)
            {
                statusTextBlock.Text = DialogTextExtension.Format("MapImport_AnalysisFailed", ex.Message);
            }
            finally
            {
                _busy = false;
                SetBusy(false, statusTextBlock.Text);
            }
        }

        private string BuildPlanSummary()
        {
            int add = _planAssets.Count(asset => asset.Status == MapImportAssetStatus.ToAdd);
            int replace = _planAssets.Count(asset => asset.Status == MapImportAssetStatus.Replace);
            int existing = _planAssets.Count(asset => asset.Status == MapImportAssetStatus.Existing);
            int conflicts = _planAssets.Count(asset => asset.Status == MapImportAssetStatus.Conflict);
            int missing = _planAssets.Count(asset => asset.Status == MapImportAssetStatus.Missing);
            return DialogTextExtension.Format("MapImport_PlanSummary", _plans.Count, add, replace, existing, conflicts, missing);
        }

        private void ReplaceMapsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateMapReplacementChoice();
            if (_reviewed)
                planSummaryTextBlock.Text = BuildPlanSummary();
            planDataGrid?.Items.Refresh();
        }

        private void UpdateMapReplacementChoice()
        {
            bool replace = replaceMapsCheckBox?.IsChecked == true;
            var selectedMapIds = mapsListView.SelectedItems.Cast<SourceMapItem>()
                .Select(item => item.MapId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (MapImportAsset asset in _plans.SelectMany(plan => plan.Assets)
                .Where(asset => asset.Kind == MapImportAssetKind.Map &&
                    selectedMapIds.Contains(Path.GetFileNameWithoutExtension(asset.RelativePath)) &&
                    (asset.Status == MapImportAssetStatus.Conflict || asset.Status == MapImportAssetStatus.Replace)))
                asset.Status = replace ? MapImportAssetStatus.Replace : MapImportAssetStatus.Conflict;
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_reviewed || _plans.Count == 0 || _source == null)
                return;

            string summary = BuildPlanSummary();
            if (MessageBox.Show(
                    DialogTextExtension.Format("MapImport_ConfirmMessage", summary, replaceMapsCheckBox.IsChecked == true ? DialogTextExtension.Get("MapImport_ReplaceNotice") : string.Empty),
                    DialogTextExtension.Get("MapImport_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _busy = true;
            SetBusy(true, DialogTextExtension.Get("MapImport_Importing"));
            try
            {
                int imported = 0;
                List<string> errors = new();
                foreach (MapImportPlan plan in _plans)
                {
                    MapImportResult result = await Task.Run(() => new MapImportService(Program.DataSource, Program.InfoManager).Import(plan, CancellationToken.None, null));
                    imported += result.AddedAssetCount;
                    if (result.Errors != null)
                        errors.AddRange(result.Errors);
                }

                MessageBox.Show(
                    errors.Count == 0
                        ? DialogTextExtension.Format("MapImport_Complete", _plans.Count, imported)
                        : DialogTextExtension.Format("MapImport_CompleteWarnings", _plans.Count, errors.Count, string.Join("\n", errors.Take(12))),
                    DialogTextExtension.Get("MapImport_CompleteTitle"), MessageBoxButton.OK,
                    errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                _allowClose = true;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(DialogTextExtension.Format("MapImport_Failed", ex.Message), DialogTextExtension.Get("MapImport_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _busy = false;
                SetBusy(false, statusTextBlock.Text);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void SetBusy(bool busy, string status)
        {
            progressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            browseButton.IsEnabled = !busy;
            sourcePathTextBox.IsEnabled = !busy;
            mapsListView.IsEnabled = !busy;
            reviewButton.IsEnabled = !busy && _source != null && mapsListView.SelectedItems.Count > 0;
            if (!string.IsNullOrWhiteSpace(status))
                statusTextBlock.Text = status;
        }

        protected override void OnClosed(EventArgs e)
        {
            _source?.Dispose();
            _source = null;
            base.OnClosed(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_busy && !_allowClose)
            {
                e.Cancel = true;
                statusTextBlock.Text = DialogTextExtension.Get("MapImport_BusyClose");
            }
            base.OnClosing(e);
        }

        private sealed class SourceMapItem
        {
            public SourceMapItem(string mapId, string relativePath, string streetName, string mapName,
                bool isAlreadyInDestination)
            {
                MapId = mapId;
                RelativePath = relativePath;
                StreetName = string.IsNullOrWhiteSpace(streetName) ? "—" : streetName;
                MapName = string.IsNullOrWhiteSpace(mapName) ? mapId : mapName;
                IsAlreadyInDestination = isAlreadyInDestination;
                SearchText = $"{MapId} {StreetName} {MapName}";
            }
            public string MapId { get; }
            public string RelativePath { get; }
            public string StreetName { get; }
            public string MapName { get; }
            public bool IsAlreadyInDestination { get; }
            public string SearchText { get; }
        }
    }
}
