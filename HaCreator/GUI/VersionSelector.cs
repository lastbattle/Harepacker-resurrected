/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
        }

        private void VersionSelector_Load(object sender, EventArgs e)
        {
            // Load and validate additional version paths from config
            LoadAdditionalVersionPaths();

            RefreshVersionList();

            // Select last used version if available
            var config = HaCreatorConfig.Load();
            if (!string.IsNullOrEmpty(config.LastUsedVersion))
            {
                for (int i = 0; i < listBox_versions.Items.Count; i++)
                {
                    if (listBox_versions.Items[i] is VersionListItem item &&
                        item.Version.Version == config.LastUsedVersion)
                    {
                        listBox_versions.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (listBox_versions.Items.Count > 0)
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

                // Features
                var features = new System.Collections.Generic.List<string>();
                if (v.Features.HasPets) features.Add("Pets");
                if (v.Features.HasMount) features.Add("Mounts");
                if (v.Features.HasAndroid) features.Add("Androids");
                if (v.Features.HasV5thJob) features.Add("5th Job");

                label_features.Text = features.Count > 0
                    ? $"Features: {string.Join(", ", features)}"
                    : "Features: Basic";

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
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void button_useWz_Click(object sender, EventArgs e)
        {
            UseWzFilesInstead = true;
            DialogResult = DialogResult.Cancel;
            Close();
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
                    // Remember if this was an external version
                    string externalPath = item.Version.IsExternal ? item.Version.DirectoryPath : null;

                    if (_versionManager.DeleteVersion(item.Version.Version))
                    {
                        // Also remove from additional paths if it was external
                        if (!string.IsNullOrEmpty(externalPath))
                        {
                            RemoveAdditionalVersionPath(externalPath);
                        }

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
                        // Save the path to config for persistence
                        SaveAdditionalVersionPath(selectedPath);

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
        /// Saves an additional version path to config for persistence
        /// </summary>
        private void SaveAdditionalVersionPath(string path)
        {
            var config = HaCreatorConfig.Load();

            // Normalize path for comparison
            string normalizedPath = Path.GetFullPath(path);

            // Don't add if already in list
            if (!config.AdditionalVersionPaths.Any(p =>
                Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                config.AdditionalVersionPaths.Add(path);
                config.Save();
            }
        }

        /// <summary>
        /// Removes an additional version path from config
        /// </summary>
        private void RemoveAdditionalVersionPath(string path)
        {
            var config = HaCreatorConfig.Load();
            string normalizedPath = Path.GetFullPath(path);

            var toRemove = config.AdditionalVersionPaths
                .FirstOrDefault(p => Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (toRemove != null)
            {
                config.AdditionalVersionPaths.Remove(toRemove);
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
