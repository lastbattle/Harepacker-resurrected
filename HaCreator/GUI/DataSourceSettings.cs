using MapleLib.Img;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    /// <summary>
    /// Settings form for configuring data source options (IMG filesystem, WZ files, Hybrid mode).
    /// </summary>
    public partial class DataSourceSettings : Form
    {
        private readonly HaCreatorConfig _config;
        private bool _configChanged;

        /// <summary>
        /// Gets whether the configuration was changed and saved
        /// </summary>
        public bool ConfigChanged => _configChanged;

        public DataSourceSettings()
        {
            _config = HaCreatorConfig.Load();
            InitializeComponent();
        }

        private void DataSourceSettings_Load(object sender, EventArgs e)
        {
            // Load current settings
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Data source mode
            switch (_config.DataSourceMode)
            {
                case DataSourceMode.ImgFileSystem:
                    radioButton_imgMode.Checked = true;
                    break;
                case DataSourceMode.WzFiles:
                    radioButton_wzMode.Checked = true;
                    break;
                case DataSourceMode.Hybrid:
                    radioButton_hybridMode.Checked = true;
                    break;
            }

            // Paths
            textBox_wzPath.Text = _config.Legacy.WzFilePath ?? "";

            // Cache settings
            numericUpDown_maxMemory.Value = _config.Cache.MaxMemoryCacheMB;
            numericUpDown_maxImages.Value = _config.Cache.MaxCachedImages;
            checkBox_memoryMappedFiles.Checked = _config.Cache.EnableMemoryMappedFiles;

            // Legacy settings
            checkBox_allowWzFallback.Checked = _config.Legacy.AllowWzFallback;
            checkBox_autoConvert.Checked = _config.Legacy.AutoConvertOnLoad;

            // Extraction settings
            numericUpDown_parallelThreads.Value = _config.Extraction.ParallelThreads;
            checkBox_generateIndex.Checked = _config.Extraction.GenerateIndex;
            checkBox_validateAfterExtract.Checked = _config.Extraction.ValidateAfterExtract;

            UpdateUIState();
        }

        private void UpdateUIState()
        {
            // Enable/disable based on selected mode
            bool isImgMode = radioButton_imgMode.Checked;
            bool isWzMode = radioButton_wzMode.Checked;
            bool isHybridMode = radioButton_hybridMode.Checked;

            // WZ path controls
            textBox_wzPath.Enabled = isWzMode || isHybridMode;
            button_browseWz.Enabled = isWzMode || isHybridMode;

            // Cache settings only apply to IMG mode
            groupBox_cache.Enabled = isImgMode || isHybridMode;

            // Legacy settings
            checkBox_allowWzFallback.Enabled = isHybridMode;
            checkBox_autoConvert.Enabled = isWzMode;

            // Update description label
            if (isImgMode)
            {
                label_modeDescription.Text = "IMG Filesystem Mode (Recommended)\n" +
                    "Loads data from extracted .img files in the filesystem. " +
                    "Faster loading, supports version control, and allows direct file editing.";
            }
            else if (isWzMode)
            {
                label_modeDescription.Text = "WZ Files Mode (Legacy)\n" +
                    "Loads data directly from MapleStory .wz archive files. " +
                    "Requires MapleStory installation. Use 'Extract New...' to migrate.";
            }
            else if (isHybridMode)
            {
                label_modeDescription.Text = "Hybrid Mode\n" +
                    "Tries IMG filesystem first, falls back to WZ files if not found. " +
                    "Useful during migration or for partial extractions.";
            }
        }

        private void radioButton_mode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUIState();
        }

        private void button_browseWz_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select MapleStory installation directory containing WZ files";
                dialog.SelectedPath = textBox_wzPath.Text;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // Validate that it contains WZ files
                    if (Directory.GetFiles(dialog.SelectedPath, "*.wz").Length == 0)
                    {
                        MessageBox.Show(
                            "The selected folder doesn't contain any .wz files.\n" +
                            "Please select a MapleStory installation directory.",
                            "Invalid Directory",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    textBox_wzPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void button_ok_Click(object sender, EventArgs e)
        {
            // Validate inputs
            if (radioButton_wzMode.Checked || radioButton_hybridMode.Checked)
            {
                if (radioButton_wzMode.Checked && string.IsNullOrWhiteSpace(textBox_wzPath.Text))
                {
                    MessageBox.Show("Please specify a WZ files directory.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Save settings
            SaveSettings();

            _configChanged = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SaveSettings()
        {
            // Data source mode
            if (radioButton_imgMode.Checked)
                _config.DataSourceMode = DataSourceMode.ImgFileSystem;
            else if (radioButton_wzMode.Checked)
                _config.DataSourceMode = DataSourceMode.WzFiles;
            else if (radioButton_hybridMode.Checked)
                _config.DataSourceMode = DataSourceMode.Hybrid;

            // Paths
            _config.Legacy.WzFilePath = textBox_wzPath.Text;

            // Cache settings
            _config.Cache.MaxMemoryCacheMB = (int)numericUpDown_maxMemory.Value;
            _config.Cache.MaxCachedImages = (int)numericUpDown_maxImages.Value;
            _config.Cache.EnableMemoryMappedFiles = checkBox_memoryMappedFiles.Checked;

            // Legacy settings
            _config.Legacy.AllowWzFallback = checkBox_allowWzFallback.Checked;
            _config.Legacy.AutoConvertOnLoad = checkBox_autoConvert.Checked;

            // Extraction settings
            _config.Extraction.ParallelThreads = (int)numericUpDown_parallelThreads.Value;
            _config.Extraction.GenerateIndex = checkBox_generateIndex.Checked;
            _config.Extraction.ValidateAfterExtract = checkBox_validateAfterExtract.Checked;

            // Ensure directories exist
            _config.EnsureDirectoriesExist();

            // Save to file
            _config.Save();
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void button_resetDefaults_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to defaults?",
                "Confirm Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // Create fresh config and reload
                var defaultConfig = new HaCreatorConfig();
                _config.DataSourceMode = defaultConfig.DataSourceMode;
                _config.ImgRootPath = defaultConfig.ImgRootPath;
                _config.Cache = new CacheConfig();
                _config.Legacy = new LegacyConfig();
                _config.Extraction = new ExtractionConfig();

                LoadSettings();
            }
        }
    }
}
