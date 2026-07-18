using HaCreator.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.EditorPanels
{
    public sealed class AssetGalleryItem : INotifyPropertyChanged
    {
        private BitmapSource image;

        public string Name { get; init; }
        public BitmapSource Image
        {
            get => image;
            set
            {
                if (ReferenceEquals(image, value))
                    return;
                image = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
            }
        }
        public object Tag { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class AssetGalleryItemEventArgs : EventArgs
    {
        public AssetGalleryItemEventArgs(AssetGalleryItem item) => Item = item;
        public AssetGalleryItem Item { get; }
    }

    public partial class AssetGallery : UserControl
    {
        private readonly ObservableCollection<AssetGalleryItem> items = new();
        private readonly ICollectionView view;

        public AssetGallery()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            AssetList.ItemsSource = items;
            view = CollectionViewSource.GetDefaultView(items);
            SearchBox.Text = string.Empty;
        }

        public event EventHandler<AssetGalleryItemEventArgs> ItemActivated;
        public event EventHandler<AssetGalleryItemEventArgs> ContextRequested;
        public event EventHandler<AssetGalleryItemEventArgs> SelectionChanged;

        public IReadOnlyList<AssetGalleryItem> Items => items;
        public AssetGalleryItem SelectedItem => AssetList.SelectedItem as AssetGalleryItem;
        public IReadOnlyList<AssetGalleryItem> SelectedItems => AssetList.SelectedItems.Cast<AssetGalleryItem>().ToList();
        public bool AllowMultipleSelection
        {
            get => AssetList.SelectionMode != SelectionMode.Single;
            set => AssetList.SelectionMode = value ? SelectionMode.Extended : SelectionMode.Single;
        }
        public bool ShowSearchBox
        {
            get => SearchBox.Visibility == Visibility.Visible;
            set => SearchBox.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public AssetGalleryItem Add(Bitmap bitmap, string name, object tag = null)
            => Add(ConvertBitmap(bitmap ?? global::HaCreator.Properties.Resources.placeholder), name, tag);

        public AssetGalleryItem Add(BitmapSource image, string name, object tag = null)
        {
            AssetGalleryItem item = new()
            {
                Name = name ?? string.Empty,
                Image = image,
                Tag = tag
            };
            items.Add(item);
            return item;
        }

        public AssetGalleryItem Add(string name, object tag = null) => Add((BitmapSource)null, name, tag);

        public void Clear()
        {
            AssetList.SelectedItem = null;
            items.Clear();
        }

        public void Remove(AssetGalleryItem item) => items.Remove(item);

        public void SelectByTags(IEnumerable<object> tags)
        {
            HashSet<object> selectedTags = new(tags ?? Enumerable.Empty<object>());
            AssetList.SelectedItems.Clear();
            foreach (AssetGalleryItem item in items)
                if (item.Tag != null && selectedTags.Contains(item.Tag))
                    AssetList.SelectedItems.Add(item);
            if (AssetList.SelectedItem is AssetGalleryItem selected)
                AssetList.ScrollIntoView(selected);
        }

        public void UpdateImage(AssetGalleryItem item, Bitmap bitmap)
        {
            if (item != null && bitmap != null)
                item.Image = ConvertBitmap(bitmap);
        }

        public void SetFilter(string query)
        {
            SearchBox.Text = query ?? string.Empty;
            view.Refresh();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text?.Trim() ?? string.Empty;
            view.Filter = candidate => candidate is AssetGalleryItem item &&
                (query.Length == 0 || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        private void AssetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedItem != null)
                SelectionChanged?.Invoke(this, new AssetGalleryItemEventArgs(SelectedItem));
        }

        private void AssetList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(AssetList, e.OriginalSource as DependencyObject) is ListBoxItem container &&
                container.DataContext is AssetGalleryItem item)
            {
                if (AllowMultipleSelection && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                    container.IsSelected = !container.IsSelected;
                else
                    AssetList.SelectedItem = item;
                ItemActivated?.Invoke(this, new AssetGalleryItemEventArgs(item));
                e.Handled = true;
            }
        }

        private void AssetList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(AssetList, e.OriginalSource as DependencyObject) is ListBoxItem container &&
                container.DataContext is AssetGalleryItem item)
            {
                if (!container.IsSelected)
                    AssetList.SelectedItem = item;
                ContextRequested?.Invoke(this, new AssetGalleryItemEventArgs(item));
                e.Handled = true;
            }
        }

        private static BitmapSource ConvertBitmap(Bitmap bitmap)
        {
            IntPtr handle = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);
    }
}
