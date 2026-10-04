using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.Cutscene
{
    public enum CutsceneAssetKind
    {
        Visual,
        Sound
    }

    public partial class CutsceneAssetPicker : Window
    {
        private IReadOnlyList<string> _assets;
        private readonly Func<string, BitmapSource> _previewResolver;
        private readonly Func<string, IEnumerable<string>> _assetGroupLoader;

        public string SelectedPath { get; private set; }

        public CutsceneAssetPicker(IEnumerable<string> assets, CutsceneAssetKind kind, Func<string, BitmapSource> previewResolver = null)
        {
            InitializeComponent();
            _assets = assets.Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _previewResolver = previewResolver;
            Title = CutsceneEditorTextExtension.Get(kind == CutsceneAssetKind.Visual
                ? "Cutscene_SelectVisualTitle"
                : "Cutscene_SelectSoundTitle");
            purposeTextBlock.Text = CutsceneEditorTextExtension.Get(kind == CutsceneAssetKind.Visual
                ? "Cutscene_SelectVisualPurpose"
                : "Cutscene_SelectSoundPurpose");
            previewImage.Visibility = kind == CutsceneAssetKind.Visual ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter();
            Loaded += (_, _) => searchTextBox.Focus();
        }

        public CutsceneAssetPicker(IEnumerable<string> assetGroups, Func<string, IEnumerable<string>> assetGroupLoader,
            string initialGroup, CutsceneAssetKind kind)
        {
            InitializeComponent();
            _assets = Array.Empty<string>();
            _assetGroupLoader = assetGroupLoader;
            Title = CutsceneEditorTextExtension.Get(kind == CutsceneAssetKind.Visual
                ? "Cutscene_SelectVisualTitle"
                : "Cutscene_SelectSoundTitle");
            purposeTextBlock.Text = CutsceneEditorTextExtension.Get(kind == CutsceneAssetKind.Visual
                ? "Cutscene_SelectVisualPurpose"
                : "Cutscene_SelectSoundPurpose");
            previewImage.Visibility = kind == CutsceneAssetKind.Visual ? Visibility.Visible : Visibility.Collapsed;
            assetGroupComboBox.Visibility = Visibility.Visible;
            assetGroupComboBox.ItemsSource = assetGroups
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            assetGroupComboBox.SelectedItem = assetGroupComboBox.Items
                .Cast<string>()
                .FirstOrDefault(group => string.Equals(group, initialGroup, StringComparison.OrdinalIgnoreCase));
            if (assetGroupComboBox.SelectedItem == null && assetGroupComboBox.Items.Count > 0)
                assetGroupComboBox.SelectedIndex = 0;
            ApplyFilter();
            Loaded += (_, _) => searchTextBox.Focus();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void AssetGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string group = assetGroupComboBox.SelectedItem as string;
            _assets = group == null || _assetGroupLoader == null
                ? Array.Empty<string>()
                : _assetGroupLoader(group)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = searchTextBox?.Text?.Trim() ?? string.Empty;
            List<string> results = _assets.Where(path => query.Length == 0 || path.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            assetListBox.ItemsSource = results;
            resultCountTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_AssetCount", results.Count);
            if (results.Count > 0)
                assetListBox.SelectedIndex = 0;
        }

        private void AssetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedPath = assetListBox.SelectedItem as string;
            selectedPathTextBlock.Text = SelectedPath ?? CutsceneEditorTextExtension.Get("Cutscene_NoAssetSelected");
            selectButton.IsEnabled = SelectedPath != null;
            previewImage.Source = SelectedPath == null || _previewResolver == null ? null : _previewResolver(SelectedPath);
        }

        private void AssetListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedPath != null)
                DialogResult = true;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPath != null)
                DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = null;
            DialogResult = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;
            SelectedPath = null;
            DialogResult = false;
        }
    }
}
