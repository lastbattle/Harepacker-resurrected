using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    /// <summary>
    /// Migration Wizard for converting WZ files to IMG filesystem format.
    /// Guides users through the conversion process step by step.
    /// </summary>
    public partial class MigrationWizard : Form
    {
        private readonly VersionManager _versionManager;
        private CancellationTokenSource _cancellationTokenSource;

        // Wizard state
        private int _currentStep = 0;
        private const int TOTAL_STEPS = 4;

        // User selections
        private string _selectedWzPath;
        private string _versionName;
        private string _displayName;
        private WzMapleVersion _encryption = WzMapleVersion.GMS;
        private List<DetectedMapleInstallation> _detectedInstallations;

        /// <summary>
        /// Gets whether the migration was completed successfully
        /// </summary>
        public bool MigrationCompleted { get; private set; }

        /// <summary>
        /// Gets the path to the newly created version
        /// </summary>
        public string CreatedVersionPath { get; private set; }

        public MigrationWizard(VersionManager versionManager)
        {
            _versionManager = versionManager ?? throw new ArgumentNullException(nameof(versionManager));
            InitializeComponent();
        }

        private void MigrationWizard_Load(object sender, EventArgs e)
        {
            ShowStep(0);
        }

        #region Step Navigation
        private void ShowStep(int step)
        {
            _currentStep = step;

            // Hide all panels
            panel_welcome.Visible = false;
            panel_selectSource.Visible = false;
            panel_configure.Visible = false;
            panel_progress.Visible = false;

            // Show current panel
            switch (step)
            {
                case 0:
                    panel_welcome.Visible = true;
                    button_back.Enabled = false;
                    button_next.Text = "Next >";
                    button_next.Enabled = true;
                    break;
                case 1:
                    panel_selectSource.Visible = true;
                    button_back.Enabled = true;
                    button_next.Text = "Next >";
                    button_next.Enabled = !string.IsNullOrEmpty(_selectedWzPath);
                    break;
                case 2:
                    panel_configure.Visible = true;
                    button_back.Enabled = true;
                    button_next.Text = "Start Extraction";
                    UpdateConfigureNextButton();
                    break;
                case 3:
                    panel_progress.Visible = true;
                    button_back.Enabled = false;
                    button_next.Text = "Cancel";
                    button_next.Enabled = true;
                    StartExtraction();
                    break;
            }

            label_stepInfo.Text = $"Step {step + 1} of {TOTAL_STEPS}";
        }

        private void button_next_Click(object sender, EventArgs e)
        {
            if (_currentStep == 3)
            {
                // Cancel extraction
                _cancellationTokenSource?.Cancel();
                return;
            }

            if (_currentStep < TOTAL_STEPS - 1)
            {
                ShowStep(_currentStep + 1);
            }
        }

        private void button_back_Click(object sender, EventArgs e)
        {
            if (_currentStep > 0)
            {
                ShowStep(_currentStep - 1);
            }
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            if (_currentStep == 3 && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                if (MessageBox.Show("Are you sure you want to cancel the extraction?", "Cancel",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
            else
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }
        #endregion

        #region Step 1: Welcome
        // No special logic needed for welcome panel
        #endregion

        #region Step 2: Select Source
        private void button_browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select MapleStory folder containing WZ files";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SetWzPath(dialog.SelectedPath);
                }
            }
        }

        private void button_scanFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder to scan for MapleStory installations";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    ScanForInstallations(dialog.SelectedPath);
                }
            }
        }

        private void ScanForInstallations(string path)
        {
            listBox_detected.Items.Clear();
            _detectedInstallations = BatchConverter.ScanForMapleInstallations(path);

            if (_detectedInstallations.Count == 0)
            {
                MessageBox.Show("No MapleStory installations found in the selected folder.",
                    "Scan Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var installation in _detectedInstallations)
            {
                listBox_detected.Items.Add(new DetectedInstallationItem(installation));
            }

            if (listBox_detected.Items.Count > 0)
            {
                listBox_detected.SelectedIndex = 0;
            }
        }

        private void listBox_detected_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox_detected.SelectedItem is DetectedInstallationItem item)
            {
                SetWzPath(item.Installation.Path);

                // Auto-fill suggested values
                textBox_versionName.Text = item.Installation.SuggestedVersionName;
                comboBox_encryption.SelectedItem = item.Installation.SuggestedEncryption;
            }
        }

        private void SetWzPath(string path)
        {
            _selectedWzPath = path;
            textBox_wzPath.Text = path;
            button_next.Enabled = !string.IsNullOrEmpty(path) && Directory.Exists(path);

            // Try to auto-detect and suggest version name
            if (string.IsNullOrEmpty(textBox_versionName.Text))
            {
                textBox_versionName.Text = Path.GetFileName(path).Replace(" ", "_").ToLowerInvariant();
            }
        }
        #endregion

        #region Step 3: Configure
        private void textBox_versionName_TextChanged(object sender, EventArgs e)
        {
            _versionName = textBox_versionName.Text.Trim();
            UpdateConfigureNextButton();
        }

        private void textBox_displayName_TextChanged(object sender, EventArgs e)
        {
            _displayName = textBox_displayName.Text.Trim();
        }

        private void comboBox_encryption_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_encryption.SelectedItem is WzMapleVersion version)
            {
                _encryption = version;
            }
        }

        private void UpdateConfigureNextButton()
        {
            bool isValid = !string.IsNullOrWhiteSpace(_versionName) &&
                           IsValidVersionName(_versionName);
            button_next.Enabled = isValid;

            if (!isValid && !string.IsNullOrWhiteSpace(_versionName))
            {
                label_versionError.Text = "Version name can only contain letters, numbers, underscores, and hyphens.";
                label_versionError.Visible = true;
            }
            else if (_versionManager.VersionExists(_versionName))
            {
                label_versionError.Text = "A version with this name already exists.";
                label_versionError.Visible = true;
                button_next.Enabled = false;
            }
            else
            {
                label_versionError.Visible = false;
            }
        }

        private bool IsValidVersionName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        }
        #endregion

        #region Step 4: Extraction Progress
        private async void StartExtraction()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            listBox_log.Items.Clear();
            progressBar_extraction.Value = 0;
            label_progress.Text = "Starting extraction...";

            var extractionService = new WzExtractionService();

            // Subscribe to events
            extractionService.ProgressChanged += (s, args) =>
            {
                BeginInvoke(new Action(() =>
                {
                    progressBar_extraction.Value = (int)args.Progress.ProgressPercentage;
                    label_progress.Text = $"{args.Progress.CurrentPhase}: {args.Progress.CurrentFile}";
                }));
            };

            extractionService.CategoryStarted += (s, args) =>
            {
                BeginInvoke(new Action(() =>
                {
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] Starting: {args.Category}");
                    listBox_log.TopIndex = listBox_log.Items.Count - 1;
                }));
            };

            extractionService.CategoryCompleted += (s, args) =>
            {
                BeginInvoke(new Action(() =>
                {
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] Completed: {args.Category} ({args.Result?.ImagesExtracted ?? 0} images)");
                    listBox_log.TopIndex = listBox_log.Items.Count - 1;
                }));
            };

            extractionService.ErrorOccurred += (s, args) =>
            {
                BeginInvoke(new Action(() =>
                {
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {args.Exception?.Message}");
                    listBox_log.TopIndex = listBox_log.Items.Count - 1;
                }));
            };

            string outputPath = Path.Combine(_versionManager.RootPath, _versionName);

            try
            {
                var result = await extractionService.ExtractAsync(
                    _selectedWzPath,
                    outputPath,
                    _versionName,
                    string.IsNullOrEmpty(_displayName) ? _versionName : _displayName,
                    _encryption,
                    resolveLinks: true,
                    _cancellationTokenSource.Token,
                    null);

                if (result.Success)
                {
                    progressBar_extraction.Value = 100;
                    label_progress.Text = "Extraction completed successfully!";
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] === Extraction Complete ===");
                    listBox_log.Items.Add($"  Categories: {result.CategoriesExtracted.Count}");
                    listBox_log.Items.Add($"  Images: {result.TotalImagesExtracted}");
                    listBox_log.Items.Add($"  Time: {result.Duration:mm\\:ss}");

                    MigrationCompleted = true;
                    CreatedVersionPath = outputPath;

                    button_next.Text = "Finish";
                    button_next.Click -= button_next_Click;
                    button_next.Click += (s, args) =>
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    };

                    MessageBox.Show($"Extraction completed successfully!\n\n" +
                        $"Categories extracted: {result.CategoriesExtracted.Count}\n" +
                        $"Images extracted: {result.TotalImagesExtracted}\n" +
                        $"Time elapsed: {result.Duration:mm\\:ss}",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    label_progress.Text = "Extraction completed with errors.";
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] === Extraction Completed with Errors ===");
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        listBox_log.Items.Add($"  Error: {result.ErrorMessage}");
                    }

                    button_next.Text = "Back";
                    button_back.Enabled = true;
                }
            }
            catch (OperationCanceledException)
            {
                label_progress.Text = "Extraction cancelled.";
                listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] === Extraction Cancelled ===");
                button_next.Text = "Back";
                button_back.Enabled = true;
            }
            catch (Exception ex)
            {
                label_progress.Text = $"Error: {ex.Message}";
                listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] FATAL ERROR: {ex.Message}");
                button_next.Text = "Back";
                button_back.Enabled = true;

                MessageBox.Show($"An error occurred during extraction:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Helper Classes
        private class DetectedInstallationItem
        {
            public DetectedMapleInstallation Installation { get; }

            public DetectedInstallationItem(DetectedMapleInstallation installation)
            {
                Installation = installation;
            }

            public override string ToString()
            {
                string format = Installation.Is64Bit ? " (64-bit)" : "";
                return $"{Installation.FolderName}{format} - {Installation.WzFileCount} WZ files";
            }
        }
        #endregion
    }
}
