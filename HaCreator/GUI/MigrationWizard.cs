using MapleLib.Img;
using MapleLib.WzLib;
using HaCreator.GUI.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    /// <summary>
    /// Migration Wizard for converting WZ files to IMG filesystem format.
    /// Guides users through the conversion process step by step.
    /// </summary>
    public partial class MigrationWizard : Window
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
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
            foreach (WzMapleVersion version in Enum.GetValues<WzMapleVersion>()) comboBox_encryption.Items.Add(version);
            comboBox_encryption.SelectedItem = _encryption;
            ShowStep(0);
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
            panel_welcome.Visibility = Visibility.Collapsed;
            panel_selectSource.Visibility = Visibility.Collapsed;
            panel_configure.Visibility = Visibility.Collapsed;
            panel_progress.Visibility = Visibility.Collapsed;

            // Show current panel
            switch (step)
            {
                case 0:
                    panel_welcome.Visibility = Visibility.Visible;
                    button_back.IsEnabled = false;
                    button_next.Content = DialogTextExtension.Get("Dialog_NextButton");
                    button_next.IsEnabled = true;
                    break;
                case 1:
                    panel_selectSource.Visibility = Visibility.Visible;
                    button_back.IsEnabled = true;
                    button_next.Content = DialogTextExtension.Get("Dialog_NextButton");
                    button_next.IsEnabled = !string.IsNullOrEmpty(_selectedWzPath);
                    break;
                case 2:
                    panel_configure.Visibility = Visibility.Visible;
                    button_back.IsEnabled = true;
                    button_next.Content = DialogTextExtension.Get("Dialog_StartExtraction");
                    UpdateConfigureNextButton();
                    break;
                case 3:
                    panel_progress.Visibility = Visibility.Visible;
                    button_back.IsEnabled = false;
                    button_next.Content = DialogTextExtension.Get("Dialog_Cancel");
                    button_next.IsEnabled = true;
                    StartExtraction();
                    break;
            }

            label_stepInfo.Text = DialogTextExtension.Format("Dialog_WizardStep", step + 1, TOTAL_STEPS);
        }

        private void button_next_Click(object sender, RoutedEventArgs e)
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

        private void button_back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                ShowStep(_currentStep - 1);
            }
        }

        private void button_cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 3 && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                if (MessageBox.Show(this, DialogTextExtension.Get("Dialog_ConfirmCancelExtraction"), DialogTextExtension.Get("Dialog_Cancel"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
            else
            {
                DialogResult = false;
            }
        }
        #endregion

        #region Step 1: Welcome
        // No special logic needed for welcome panel
        #endregion

        #region Step 2: Select Source
        private void button_browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = DialogTextExtension.Get("Dialog_SelectMapleWzFolder");
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    SetWzPath(dialog.SelectedPath);
                }
            }
        }

        private void button_scanFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = DialogTextExtension.Get("Dialog_SelectScanFolder");

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
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
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_NoMapleInstallations"),
                    DialogTextExtension.Get("Dialog_ScanComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void listBox_detected_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
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
            button_next.IsEnabled = !string.IsNullOrEmpty(path) && Directory.Exists(path);

            // Try to auto-detect and suggest version name
            if (string.IsNullOrEmpty(textBox_versionName.Text))
            {
                textBox_versionName.Text = Path.GetFileName(path).Replace(" ", "_").ToLowerInvariant();
            }
        }
        #endregion

        #region Step 3: Configure
        private void textBox_versionName_TextChanged(object sender, TextChangedEventArgs e)
        {
            _versionName = textBox_versionName.Text.Trim();
            UpdateConfigureNextButton();
        }

        private void textBox_displayName_TextChanged(object sender, TextChangedEventArgs e)
        {
            _displayName = textBox_displayName.Text.Trim();
        }

        private void comboBox_encryption_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
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
            button_next.IsEnabled = isValid;

            if (!isValid && !string.IsNullOrWhiteSpace(_versionName))
            {
                label_versionError.Text = DialogTextExtension.Get("Dialog_InvalidVersionNameChars");
                label_versionError.Visibility = Visibility.Visible;
            }
            else if (_versionManager.VersionExists(_versionName))
            {
                label_versionError.Text = DialogTextExtension.Get("Dialog_VersionExists");
                label_versionError.Visibility = Visibility.Visible;
                button_next.IsEnabled = false;
            }
            else
            {
                label_versionError.Visibility = Visibility.Collapsed;
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
            label_progress.Text = DialogTextExtension.Get("Dialog_StartingExtraction");

            var extractionService = new WzExtractionService();

            // Subscribe to events
            extractionService.ProgressChanged += (s, args) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    progressBar_extraction.Value = (int)args.Progress.ProgressPercentage;
                    label_progress.Text = $"{args.Progress.CurrentPhase}: {args.Progress.CurrentFile}";
                }));
            };

            extractionService.CategoryStarted += (s, args) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_LogStartingCategory", DateTime.Now, args.Category));
                    listBox_log.ScrollIntoView(listBox_log.Items[listBox_log.Items.Count - 1]);
                }));
            };

            extractionService.CategoryCompleted += (s, args) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_LogCompletedCategory", DateTime.Now, args.Category, args.Result?.ImagesExtracted ?? 0));
                    listBox_log.ScrollIntoView(listBox_log.Items[listBox_log.Items.Count - 1]);
                }));
            };

            extractionService.ErrorOccurred += (s, args) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {args.Exception?.Message}");
                    listBox_log.ScrollIntoView(listBox_log.Items[listBox_log.Items.Count - 1]);
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
                    resolveLinks: false,
                    _cancellationTokenSource.Token,
                    null);

                if (result.Success)
                {
                    progressBar_extraction.Value = 100;
                    label_progress.Text = DialogTextExtension.Get("Dialog_ExtractionSuccess");
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] === Extraction Complete ===");
                    listBox_log.Items.Add($"  Categories: {result.CategoriesExtracted.Count}");
                    listBox_log.Items.Add($"  Images: {result.TotalImagesExtracted}");
                    listBox_log.Items.Add($"  Time: {result.Duration:mm\\:ss}");

                    MigrationCompleted = true;
                    CreatedVersionPath = outputPath;

                    button_next.Content = DialogTextExtension.Get("Dialog_Finish");
                    button_next.Click -= button_next_Click;
                    button_next.Click += (s, args) =>
                    {
                        DialogResult = true;
                    };

                    MessageBox.Show(this, DialogTextExtension.Format("Dialog_ExtractionSuccessSummary",
                            result.CategoriesExtracted.Count, result.TotalImagesExtracted, result.Duration),
                        DialogTextExtension.Get("Dialog_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    label_progress.Text = DialogTextExtension.Get("Dialog_ExtractionCompletedErrors");
                    listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] === Extraction Completed with Errors ===");
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        listBox_log.Items.Add($"  Error: {result.ErrorMessage}");
                    }

                    button_next.Content = DialogTextExtension.Get("Dialog_Back");
                    button_back.IsEnabled = true;
                }
            }
            catch (OperationCanceledException)
            {
                label_progress.Text = DialogTextExtension.Get("Dialog_ExtractionCancelled");
                listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] === Extraction Cancelled ===");
                button_next.Content = DialogTextExtension.Get("Dialog_Back");
                button_back.IsEnabled = true;
            }
            catch (Exception ex)
            {
                label_progress.Text = DialogTextExtension.Format("Dialog_ErrorWithMessage", ex.Message);
                listBox_log.Items.Add($"[{DateTime.Now:HH:mm:ss}] FATAL ERROR: {ex.Message}");
                button_next.Content = DialogTextExtension.Get("Dialog_Back");
                button_back.IsEnabled = true;

                MessageBox.Show(this, DialogTextExtension.Format("Dialog_ExtractionException", ex.Message),
                    DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
