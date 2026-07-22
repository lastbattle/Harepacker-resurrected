using HaCreator.Properties;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        internal Func<(Bitmap Image, object Tag)> ContentLoader { get; set; }
        internal bool IsContentLoaded { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class AssetGalleryItemEventArgs : EventArgs
    {
        public AssetGalleryItemEventArgs(AssetGalleryItem item) => Item = item;
        public AssetGalleryItem Item { get; }
    }

    public partial class AssetGallery : UserControl
    {
        private readonly BulkObservableCollection<AssetGalleryItem> items = new();
        private readonly ICollectionView view;
        private readonly DispatcherTimer filterTimer;
        private readonly Queue<AssetGalleryItem> contentLoadQueue = new();
        private readonly HashSet<AssetGalleryItem> queuedContentLoads = new();
        private bool settingFilter;
        private bool contentLoadScheduled;

        public AssetGallery()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            AssetList.ItemsSource = items;
            view = CollectionViewSource.GetDefaultView(items);
            filterTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            filterTimer.Tick += (_, _) =>
            {
                filterTimer.Stop();
                ApplyFilter();
            };
            SearchBox.Text = string.Empty;
        }

        public event EventHandler<AssetGalleryItemEventArgs> ItemActivated;
        public event EventHandler<AssetGalleryItemEventArgs> ContextRequested;
        public event EventHandler<AssetGalleryItemEventArgs> SelectionChanged;
        public event EventHandler<AssetGalleryItemEventArgs> ItemRealized;

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

        public AssetGalleryItem AddLazy(string name, Func<(Bitmap Image, object Tag)> contentLoader)
        {
            AssetGalleryItem item = new()
            {
                Name = name ?? string.Empty,
                ContentLoader = contentLoader ?? throw new ArgumentNullException(nameof(contentLoader))
            };
            items.Add(item);
            return item;
        }

        public void Clear()
        {
            contentLoadQueue.Clear();
            queuedContentLoads.Clear();
            contentLoadScheduled = false;
            AssetList.SelectedItem = null;
            items.Clear();
        }

        public void Remove(AssetGalleryItem item) => items.Remove(item);

        public IDisposable DeferUpdates()
        {
            IDisposable viewDeferral = view.DeferRefresh();
            IDisposable collectionDeferral = items.DeferNotifications();
            return new CompositeDisposable(collectionDeferral, viewDeferral);
        }

        public bool IsRealized(AssetGalleryItem item) =>
            item != null && AssetList.ItemContainerGenerator.ContainerFromItem(item) != null;

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
            {
                item.ContentLoader = null;
                item.IsContentLoaded = true;
                item.Image = ConvertBitmap(bitmap);
            }
        }

        public void SetFilter(string query)
        {
            filterTimer.Stop();
            settingFilter = true;
            SearchBox.Text = query ?? string.Empty;
            settingFilter = false;
            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (settingFilter)
                return;
            filterTimer.Stop();
            filterTimer.Start();
        }

        private void ApplyFilter()
        {
            string query = SearchBox.Text?.Trim() ?? string.Empty;
            view.Filter = candidate => candidate is AssetGalleryItem item &&
                (query.Length == 0 || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        private void AssetTile_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBoxItem { DataContext: AssetGalleryItem item })
            {
                QueueContentLoad(item);
                ItemRealized?.Invoke(this, new AssetGalleryItemEventArgs(item));
            }
        }

        private void QueueContentLoad(AssetGalleryItem item)
        {
            if (item?.ContentLoader == null || item.IsContentLoaded || !queuedContentLoads.Add(item))
                return;

            contentLoadQueue.Enqueue(item);
            ScheduleContentLoad();
        }

        private void ScheduleContentLoad()
        {
            if (contentLoadScheduled || contentLoadQueue.Count == 0)
                return;

            contentLoadScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ProcessNextContentLoad));
        }

        private void ProcessNextContentLoad()
        {
            contentLoadScheduled = false;
            while (contentLoadQueue.Count > 0)
            {
                AssetGalleryItem item = contentLoadQueue.Dequeue();
                queuedContentLoads.Remove(item);
                if (!items.Contains(item) || !IsRealized(item))
                    continue;

                EnsureContentLoaded(item);
                break;
            }
            ScheduleContentLoad();
        }

        private bool EnsureContentLoaded(AssetGalleryItem item)
        {
            if (item == null || item.IsContentLoaded || item.ContentLoader == null)
                return item != null;

            try
            {
                (Bitmap bitmap, object tag) = item.ContentLoader();
                item.IsContentLoaded = true;
                item.ContentLoader = null;
                item.Tag = tag;
                if (bitmap == null && tag == null)
                {
                    items.Remove(item);
                    return false;
                }
                item.Image = ConvertBitmap(bitmap ?? global::HaCreator.Properties.Resources.placeholder);
                return true;
            }
            catch (Exception exception)
            {
                item.IsContentLoaded = true;
                item.ContentLoader = null;
                Debug.WriteLine($"Unable to load asset thumbnail {item.Name}: {exception.Message}");
                item.Image = ConvertBitmap(global::HaCreator.Properties.Resources.placeholder);
                return true;
            }
        }

        private void AssetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedItem != null && EnsureContentLoaded(SelectedItem))
                SelectionChanged?.Invoke(this, new AssetGalleryItemEventArgs(SelectedItem));
        }

        private void AssetList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(AssetList, e.OriginalSource as DependencyObject) is ListBoxItem container &&
                container.DataContext is AssetGalleryItem item)
            {
                if (!EnsureContentLoaded(item))
                    return;
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
                if (!EnsureContentLoaded(item))
                    return;
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

        private sealed class CompositeDisposable : IDisposable
        {
            private IDisposable first;
            private IDisposable second;

            public CompositeDisposable(IDisposable first, IDisposable second)
            {
                this.first = first;
                this.second = second;
            }

            public void Dispose()
            {
                first?.Dispose();
                first = null;
                second?.Dispose();
                second = null;
            }
        }
    }

    internal sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private int deferLevel;
        private bool changed;

        public IDisposable DeferNotifications()
        {
            deferLevel++;
            return new NotificationDeferral(this);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (deferLevel > 0)
            {
                changed = true;
                return;
            }
            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (deferLevel > 0)
            {
                changed = true;
                return;
            }
            base.OnPropertyChanged(e);
        }

        private void EndDeferral()
        {
            if (--deferLevel != 0 || !changed)
                return;

            changed = false;
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            base.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private sealed class NotificationDeferral : IDisposable
        {
            private BulkObservableCollection<T> owner;

            public NotificationDeferral(BulkObservableCollection<T> owner) => this.owner = owner;

            public void Dispose()
            {
                owner?.EndDeferral();
                owner = null;
            }
        }
    }
}
