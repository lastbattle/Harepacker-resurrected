using MapleLib.Img;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    /// <summary>
    /// Form for selecting an IMG filesystem version to load
    /// </summary>
    public partial class VersionSelector : Form
    {
        private readonly VersionManager _versionManager;

        /// <summary>
        /// Gets the selected version, or null if cancelled
        /// </summary>
        public VersionInfo SelectedVersion { get; private set; }

        /// <summary>
        /// Gets whether the user chose to use WZ files instead
        /// </summary>
        public bool UseWzFilesInstead { get; private set; }

        /// <summary>
        /// Gets whether the user chose to extract new version
        /// </summary>
        public bool ExtractNewVersion { get; private set; }

        /// <summary>
        /// Creates a new VersionSelector
        /// </summary>
        public VersionSelector(VersionManager versionManager)
        {
            _versionManager = versionManager ?? throw new ArgumentNullException(nameof(versionManager));
            InitializeComponent();

            // Subscribe to hot swap events
            SubscribeToHotSwapEvents();
        }

        /// <summary>
        /// Subscribes to hot swap events from the VersionManager
        /// </summary>
        private void SubscribeToHotSwapEvents()
        {
            _versionManager.VersionsChanged += OnVersionsChanged;
        }

        /// <summary>
        /// Handles version list changes from hot swap
        /// </summary>
        private void OnVersionsChanged(object sender, VersionsChangedEventArgs e)
        {
            // Marshal to UI thread
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HandleVersionChange(e)));
            }
            else
            {
                HandleVersionChange(e);
            }
        }

        /// <summary>
        /// Handles the version change on the UI thread
        /// </summary>
        private void HandleVersionChange(VersionsChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case VersionChangeType.Added:
                    if (e.AffectedVersion != null)
                    {
                        // Add new version to the list
                        var newItem = new VersionListItem(e.AffectedVersion);
                        listBox_versions.Items.Add(newItem);
                        label_noVersions.Visible = false;

                        // Sort the list
                        SortVersionList();
                    }
                    break;

                case VersionChangeType.Removed:
                    if (e.AffectedVersion != null)
                    {
                        // Find and remove the version from the list
                        for (int i = listBox_versions.Items.Count - 1; i >= 0; i--)
                        {
                            if (listBox_versions.Items[i] is VersionListItem item &&
                                item.Version.DirectoryPath.Equals(e.AffectedVersion.DirectoryPath, StringComparison.OrdinalIgnoreCase))
                            {
                                bool wasSelected = listBox_versions.SelectedIndex == i;
                                listBox_versions.Items.RemoveAt(i);

                                if (wasSelected && listBox_versions.Items.Count > 0)
                                {
                                    listBox_versions.SelectedIndex = Math.Min(i, listBox_versions.Items.Count - 1);
                                }
                                break;
                            }
                        }

                        if (listBox_versions.Items.Count == 0)
                        {
                            label_noVersions.Visible = true;
                            panel_details.Visible = false;
                        }
                    }
                    break;

                case VersionChangeType.Refreshed:
                    RefreshVersionList();
                    break;
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// Sorts the version list alphabetically
        /// </summary>
        private void SortVersionList()
        {
            var items = listBox_versions.Items.Cast<VersionListItem>()
                .OrderBy(i => i.Version.Version)
                .ToList();

            var selectedItem = listBox_versions.SelectedItem;
            listBox_versions.Items.Clear();

            foreach (var item in items)
            {
                listBox_versions.Items.Add(item);
            }

            if (selectedItem != null)
            {
                listBox_versions.SelectedItem = selectedItem;
            }
        }

        private void VersionSelector_Load(object sender, EventArgs e)
        {
            // Load and validate additional version paths from config
            LoadAdditionalVersionPaths();

            // Load recent version paths from history
            LoadRecentVersionPaths();

            RefreshVersionList();

            // Select last used version if available, otherwise select first item
            var config = HaCreatorConfig.Load();
            bool foundLastUsed = false;

            if (!string.IsNullOrEmpty(config.LastUsedVersion))
            {
                for (int i = 0; i < listBox_versions.Items.Count; i++)
                {
                    if (listBox_versions.Items[i] is VersionListItem item &&
                        item.Version.Version == config.LastUsedVersion)
                    {
                        listBox_versions.SelectedIndex = i;
                        foundLastUsed = true;
                        break;
                    }
                }
            }

            // Always select first item if nothing was selected
            if (!foundLastUsed && listBox_versions.Items.Count > 0)
            {
                listBox_versions.SelectedIndex = 0;
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// Loads additional version paths from config and validates they still exist
        /// </summary>
        private void LoadAdditionalVersionPaths()
        {
            var config = HaCreatorConfig.Load();
            bool configChanged = false;

            // Validate each path and remove missing ones
            var pathsToRemove = new System.Collections.Generic.List<string>();
            foreach (var path in config.AdditionalVersionPaths)
            {
                if (Directory.Exists(path))
                {
                    // Try to add the version
                    _versionManager.AddExternalVersion(path);
                }
                else
                {
                    // Mark for removal
                    pathsToRemove.Add(path);
                    configChanged = true;
                }
            }

            // Remove missing paths from config
            if (configChanged)
            {
                foreach (var path in pathsToRemove)
                {
                    config.AdditionalVersionPaths.Remove(path);
                }
                config.Save();
            }
        }

        /// <summary>
        /// Loads recent version paths from history and validates they still exist
        /// </summary>
        private void LoadRecentVersionPaths()
        {
            var config = HaCreatorConfig.Load();
            bool configChanged = false;

            // Validate each path and remove missing ones
            var pathsToRemove = new System.Collections.Generic.List<string>();
            foreach (var path in config.RecentVersionPaths)
            {
                if (Directory.Exists(path))
                {
                    // Try to add the version (won't duplicate if already added)
                    _versionManager.AddExternalVersion(path);
                }
                else
                {
                    // Mark for removal
                    pathsToRemove.Add(path);
                    configChanged = true;
                }
            }

            // Remove missing paths from config
            if (configChanged)
            {
                foreach (var path in pathsToRemove)
                {
                    config.RecentVersionPaths.Remove(path);
                }
                config.Save();
            }
        }

        private void RefreshVersionList()
        {
            listBox_versions.Items.Clear();
            _versionManager.Refresh();

            foreach (var version in _versionManager.AvailableVersions)
            {
                listBox_versions.Items.Add(new VersionListItem(version));
            }

            if (listBox_versions.Items.Count == 0)
            {
                label_noVersions.Visible = true;
                panel_details.Visible = false;
            }
            else
            {
                label_noVersions.Visible = false;
            }

            UpdateButtonStates();
        }

        private void listBox_versions_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVersionDetails();
            UpdateButtonStates();
        }

        private void UpdateVersionDetails()
        {
            if (listBox_versions.SelectedItem is VersionListItem item)
            {
                panel_details.Visible = true;

                var v = item.Version;
                label_versionName.Text = v.DisplayName ?? v.Version;
                label_extractedDate.Text = $"Extracted: {v.ExtractedDate:yyyy-MM-dd HH:mm}";
                label_encryption.Text = $"Encryption: {v.Encryption}";
                label_format.Text = v.Is64Bit ? "Format: 64-bit" : (v.IsPreBB ? "Format: Pre-BB" : "Format: Standard");

                // Category counts
                int totalImages = v.Categories.Values.Sum(c => c.FileCount);
                label_imageCount.Text = $"Total Images: {totalImages:N0}";
                label_categoryCount.Text = $"Categories: {v.Categories.Count}";

                // Features (reserved for future use)
                label_features.Text = "Features: -";

                // Validation status
                if (!v.IsValid && v.ValidationErrors.Count > 0)
                {
                    label_validationStatus.Text = $"Warning: {v.ValidationErrors.First()}";
                    label_validationStatus.ForeColor = Color.OrangeRed;
                }
                else
                {
                    label_validationStatus.Text = "Status: Valid";
                    label_validationStatus.ForeColor = Color.Green;
                }
            }
            else
            {
                panel_details.Visible = false;
            }
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = listBox_versions.SelectedItem != null;
            button_select.Enabled = hasSelection;
            button_delete.Enabled = hasSelection;
        }

        private void button_select_Click(object sender, EventArgs e)
        {
            if (listBox_versions.SelectedItem is VersionListItem item)
            {
                SelectedVersion = item.Version;

                // Save to config for next time
                var config = HaCreatorConfig.Load();
                config.LastUsedVersion = item.Version.Version;
                config.AddToRecentVersionPaths(item.Version.DirectoryPath);
                config.Save();

                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void button_extract_Click(object sender, EventArgs e)
        {
            ExtractNewVersion = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void button_delete_Click(object sender, EventArgs e)
        {
            if (listBox_versions.SelectedItem is VersionListItem item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete version '{item.Version.DisplayName}'?\n\nThis will permanently delete all extracted IMG files.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // Remember the path for cleanup
                    string versionPath = item.Version.DirectoryPath;

                    if (_versionManager.DeleteVersion(item.Version.Version))
                    {
                        // Remove from config lists
                        RemoveVersionPathFromConfig(versionPath);

                        RefreshVersionList();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete version. The files may be in use.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void button_refresh_Click(object sender, EventArgs e)
        {
            RefreshVersionList();
        }

        private void button_browse_Click(object sender, EventArgs e)
        {
            using (var folderBrowser = new FolderBrowserDialog())
            {
                folderBrowser.Description = "Select a folder containing extracted IMG files";
                folderBrowser.ShowNewFolderButton = false;

                if (folderBrowser.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowser.SelectedPath;

                    // Check if it looks like a valid version folder
                    bool hasManifest = File.Exists(Path.Combine(selectedPath, "manifest.json"));
                    bool hasStringFolder = Directory.Exists(Path.Combine(selectedPath, "String"));
                    bool hasMapFolder = Directory.Exists(Path.Combine(selectedPath, "Map"));

                    if (!hasManifest && !hasStringFolder && !hasMapFolder)
                    {
                        MessageBox.Show(
                            "The selected folder doesn't appear to contain extracted IMG files.\n\n" +
                            "A valid version folder should contain:\n" +
                            "- A manifest.json file, OR\n" +
                            "- String/ and Map/ folders with .img files",
                            "Invalid Folder",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    // Add as external version
                    var version = _versionManager.AddExternalVersion(selectedPath);
                    if (version != null)
                    {
                        // Save to config: add to both additional paths and recent history
                        var config = HaCreatorConfig.Load();

                        // Add to additional paths (for Browse-added folders)
                        string normalizedPath = Path.GetFullPath(selectedPath);
                        if (!config.AdditionalVersionPaths.Any(p =>
                            Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            config.AdditionalVersionPaths.Add(selectedPath);
                        }

                        // Add to recent history
                        config.AddToRecentVersionPaths(selectedPath);
                        config.Save();

                        // Add to listbox directly
                        var newItem = new VersionListItem(version);
                        listBox_versions.Items.Add(newItem);
                        listBox_versions.SelectedItem = newItem;

                        // Hide "no versions" label if it was showing
                        label_noVersions.Visible = false;

                        UpdateVersionDetails();
                        UpdateButtonStates();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to add the version. It may already be in the list.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void listBox_versions_DoubleClick(object sender, EventArgs e)
        {
            if (listBox_versions.SelectedItem != null)
            {
                button_select_Click(sender, e);
            }
        }

        /// <summary>
        /// Removes a version path from all config lists (additional paths and recent history)
        /// </summary>
        private void RemoveVersionPathFromConfig(string path)
        {
            var config = HaCreatorConfig.Load();
            string normalizedPath = Path.GetFullPath(path);
            bool changed = false;

            // Remove from additional paths
            var toRemoveAdditional = config.AdditionalVersionPaths
                .FirstOrDefault(p => Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (toRemoveAdditional != null)
            {
                config.AdditionalVersionPaths.Remove(toRemoveAdditional);
                changed = true;
            }

            // Remove from recent paths
            var toRemoveRecent = config.RecentVersionPaths
                .FirstOrDefault(p => Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (toRemoveRecent != null)
            {
                config.RecentVersionPaths.Remove(toRemoveRecent);
                changed = true;
            }

            if (changed)
            {
                config.Save();
            }
        }

        /// <summary>
        /// List item wrapper for VersionInfo
        /// </summary>
        private class VersionListItem
        {
            public VersionInfo Version { get; }

            public VersionListItem(VersionInfo version)
            {
                Version = version;
            }

            public override string ToString()
            {
                return Version.DisplayName ?? Version.Version;
            }
        }
    }
}
