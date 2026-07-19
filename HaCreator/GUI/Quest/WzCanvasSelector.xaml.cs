using HaCreator.GUI.InstanceEditor;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI.Quest
{
    /// <summary>
    /// Browses canvas properties from the active data source and returns a path usable by the #f token.
    /// </summary>
    public partial class WzCanvasSelector : Window
    {
        private const string LazyNodeMarker = "__lazy__";
        private string loadedCategory = string.Empty;
        private string loadedImageName = string.Empty;

        public string SelectedCanvasPath { get; private set; } = string.Empty;

        public WzCanvasSelector()
        {
            InitializeComponent();
            Loaded += WzCanvasSelector_Loaded;
        }

        private void WzCanvasSelector_Loaded(object sender, RoutedEventArgs e)
        {
            if (Program.DataSource == null)
            {
                MessageBox.Show(this, "No WZ/IMG data source is loaded.", "Select WZ image", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }

            CategoryCombo.ItemsSource = Program.DataSource.GetCategories().OrderBy(category => category).ToList();
            string preferredCategory = CategoryCombo.Items.Cast<string>()
                .FirstOrDefault(category => string.Equals(category, "UI", StringComparison.OrdinalIgnoreCase));
            CategoryCombo.SelectedItem = preferredCategory ?? CategoryCombo.Items.Cast<string>().FirstOrDefault();
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PropertyTree.Items.Clear();
            CanvasPreview.Source = null;
            SelectedPathText.Text = string.Empty;
            SelectButton.IsEnabled = false;

            if (CategoryCombo.SelectedItem is not string category || Program.DataSource == null)
                return;

            try
            {
                ImageCombo.ItemsSource = Program.DataSource.GetImagesInCategory(category)
                    .Select(image => image.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();
                ImageCombo.SelectedIndex = ImageCombo.Items.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to list images", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryCombo.SelectedItem is not string category)
                return;
            string imageName = ImageCombo.Text?.Trim();
            if (string.IsNullOrEmpty(imageName))
                return;

            try
            {
                WzImage image = Program.DataSource.GetImage(category, imageName);
                if (image == null)
                {
                    MessageBox.Show(this, "The selected IMG file could not be loaded.", "Select WZ image", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                image.ParseImage();
                loadedCategory = category;
                loadedImageName = imageName;
                PropertyTree.Items.Clear();
                foreach (WzImageProperty property in image.WzProperties)
                {
                    if (property != null)
                        PropertyTree.Items.Add(CreateTreeNode(property, property.Name));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to load image", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static TreeViewItem CreateTreeNode(WzImageProperty property, string path)
        {
            TreeViewItem item = new()
            {
                Header = $"{property.Name}  [{property.PropertyType}]",
                Tag = new CanvasNode(property, path)
            };
            if (property.WzProperties is { Count: > 0 })
            {
                item.Items.Add(LazyNodeMarker);
                item.Expanded += TreeNode_Expanded;
            }
            return item;
        }

        private static void TreeNode_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem item || item.Tag is not CanvasNode node ||
                item.Items.Count != 1 || item.Items[0] is not string marker || marker != LazyNodeMarker)
                return;

            item.Items.Clear();
            if (node.Property.WzProperties == null)
                return;

            foreach (WzImageProperty child in node.Property.WzProperties)
            {
                if (child != null)
                    item.Items.Add(CreateTreeNode(child, $"{node.Path}/{child.Name}"));
            }
        }

        private void PropertyTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            CanvasPreview.Source = null;
            SelectedPathText.Text = string.Empty;
            CanvasInfoText.Text = string.Empty;
            SelectButton.IsEnabled = false;

            if (PropertyTree.SelectedItem is not TreeViewItem item || item.Tag is not CanvasNode node)
                return;

            WzCanvasProperty canvas = node.Property.GetLinkedWzImageProperty() as WzCanvasProperty;
            if (canvas == null)
            {
                CanvasInfoText.Text = $"{node.Property.PropertyType} property - select a Canvas node.";
                return;
            }

            try
            {
                CanvasPreview.Source = SelectorDialogSupport.ToBitmapSource(canvas.GetLinkedWzCanvasBitmap());
                SelectedCanvasPath = $"{loadedCategory}/{loadedImageName}/{node.Path}";
                SelectedPathText.Text = SelectedCanvasPath;
                CanvasInfoText.Text = $"{canvas.PngProperty.Width} x {canvas.PngProperty.Height} - {SelectedCanvasPath}";
                SelectButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CanvasInfoText.Text = ex.Message;
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedCanvasPath))
                return;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedCanvasPath = string.Empty;
            DialogResult = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SelectedCanvasPath = string.Empty;
                DialogResult = false;
            }
        }

        private sealed class CanvasNode
        {
            public CanvasNode(WzImageProperty property, string path)
            {
                Property = property;
                Path = path;
            }

            public WzImageProperty Property { get; }
            public string Path { get; }
        }
    }
}
