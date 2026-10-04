using MapleLib.Img;
using System;
using System.IO;
using System.Windows;
using HaCreator.GUI.Localization;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    public sealed class DataSourceSettings : IDisposable
    {
        private readonly DataSourceSettingsWindow window = new DataSourceSettingsWindow();
        public bool ConfigChanged => window.ConfigChanged;
        public Forms.DialogResult ShowDialog(Forms.IWin32Window owner = null) => window.ShowDialog() == true ? Forms.DialogResult.OK : Forms.DialogResult.Cancel;
        public void Dispose() { if (window.IsLoaded) window.Close(); }
    }

    internal partial class DataSourceSettingsWindow : Window
    {
        private readonly HaCreatorConfig config = HaCreatorConfig.Load();
        internal bool ConfigChanged { get; private set; }

        internal DataSourceSettingsWindow()
        {
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
            LoadSettings();
        }

        private void LoadSettings()
        {
            imgMode.IsChecked = config.DataSourceMode == DataSourceMode.ImgFileSystem;
            wzMode.IsChecked = config.DataSourceMode == DataSourceMode.WzFiles;
            hybridMode.IsChecked = config.DataSourceMode == DataSourceMode.Hybrid;
            wzPath.Text = config.Legacy.WzFilePath ?? string.Empty;
            maxMemory.Text = config.Cache.MaxMemoryCacheMB.ToString(); maxImages.Text = config.Cache.MaxCachedImages.ToString();
            memoryMapped.IsChecked = config.Cache.EnableMemoryMappedFiles; allowFallback.IsChecked = config.Legacy.AllowWzFallback;
            autoConvert.IsChecked = config.Legacy.AutoConvertOnLoad; parallelThreads.Text = config.Extraction.ParallelThreads.ToString();
            generateIndex.IsChecked = config.Extraction.GenerateIndex; validateExtract.IsChecked = config.Extraction.ValidateAfterExtract;
            UpdateMode();
        }

        private void Mode_Checked(object sender, RoutedEventArgs e) => UpdateMode();
        private void UpdateMode()
        {
            if (modeDescription == null) return;
            bool img = imgMode.IsChecked == true, wz = wzMode.IsChecked == true, hybrid = hybridMode.IsChecked == true;
            wzPath.IsEnabled = browseWz.IsEnabled = wz || hybrid; cacheCard.IsEnabled = img || hybrid;
            allowFallback.IsEnabled = hybrid; autoConvert.IsEnabled = wz;
            modeDescription.Text = DialogTextExtension.Get(img ? "Dialog_ImgModeDescription" :
                wz ? "Dialog_WzModeDescription" : "Dialog_HybridModeDescription");
        }

        private void BrowseWz_Click(object sender, RoutedEventArgs e)
        {
            using Forms.FolderBrowserDialog dialog = new Forms.FolderBrowserDialog { Description = DialogTextExtension.Get("Dialog_SelectMapleWzFolder"), SelectedPath = wzPath.Text };
            if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
            if (Directory.GetFiles(dialog.SelectedPath, "*.wz").Length == 0)
            { MessageBox.Show(this, DialogTextExtension.Get("Dialog_NoWzFiles"), DialogTextExtension.Get("Dialog_InvalidDirectory"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            wzPath.Text = dialog.SelectedPath;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, DialogTextExtension.Get("Dialog_ConfirmResetDataSource"), DialogTextExtension.Get("Dialog_ConfirmReset"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            HaCreatorConfig defaults = new HaCreatorConfig(); config.DataSourceMode = defaults.DataSourceMode; config.ImgRootPath = defaults.ImgRootPath;
            config.Cache = new CacheConfig(); config.Legacy = new LegacyConfig(); config.Extraction = new ExtractionConfig(); LoadSettings();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (wzMode.IsChecked == true && string.IsNullOrWhiteSpace(wzPath.Text))
            { MessageBox.Show(this, DialogTextExtension.Get("Dialog_SpecifyWzDirectory"), DialogTextExtension.Get("Dialog_ValidationError"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!int.TryParse(maxMemory.Text, out int memory) || !int.TryParse(maxImages.Text, out int images) || !int.TryParse(parallelThreads.Text, out int threads))
            { MessageBox.Show(this, DialogTextExtension.Get("Dialog_CacheThreadsIntegers"), DialogTextExtension.Get("Dialog_ValidationError"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            config.DataSourceMode = imgMode.IsChecked == true ? DataSourceMode.ImgFileSystem : wzMode.IsChecked == true ? DataSourceMode.WzFiles : DataSourceMode.Hybrid;
            config.Legacy.WzFilePath = wzPath.Text; config.Cache.MaxMemoryCacheMB = memory; config.Cache.MaxCachedImages = images;
            config.Cache.EnableMemoryMappedFiles = memoryMapped.IsChecked == true; config.Legacy.AllowWzFallback = allowFallback.IsChecked == true;
            config.Legacy.AutoConvertOnLoad = autoConvert.IsChecked == true; config.Extraction.ParallelThreads = threads;
            config.Extraction.GenerateIndex = generateIndex.IsChecked == true; config.Extraction.ValidateAfterExtract = validateExtract.IsChecked == true;
            config.EnsureDirectoriesExist(); config.Save(); ConfigChanged = true; DialogResult = true;
        }
    }
}
