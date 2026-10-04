using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HaCreator.GUI.EditorPanels
{
    public partial class LifePanel : UserControl
    {
        private enum LifeEntryType
        {
            Mob,
            Npc,
            Reactor
        }

        private sealed record LifeEntry(string Id, string Name, LifeEntryType Type)
        {
            // Keep the IMG ID visible/searchable, including its usual leading zeros.
            public string DisplayName => string.IsNullOrWhiteSpace(Name)
                ? Id.PadLeft(7, '0')
                : $"{Name} ({Id.PadLeft(7, '0')})";
        }

        private readonly List<LifeEntry> reactors = new();
        private readonly List<LifeEntry> npcs = new();
        private readonly List<LifeEntry> mobs = new();

        private HaCreatorStateManager hcsm;
        private HotSwapRefreshService hotSwapService;
        private readonly Bitmap placeholderBitmap;
        private readonly BitmapSource placeholderSource;
        private int thumbnailLoadVersion;
        private bool initializingImageFilter;
        private readonly Queue<(AssetGalleryItem Item, int Version)> thumbnailQueue = new();
        private readonly HashSet<AssetGalleryItem> queuedThumbnails = new();
        private readonly HashSet<AssetGalleryItem> loadedThumbnails = new();
        private bool thumbnailPumpScheduled;

        public LifePanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            placeholderBitmap = global::HaCreator.Properties.Resources.placeholder;
            placeholderSource = ConvertBitmap(placeholderBitmap);
            lifeGallery.ItemRealized += LifeGallery_ItemRealized;
            initializingImageFilter = true;
            hideEntriesWithoutImagesCheckBox.IsChecked = ApplicationSettings.HideLifeEntriesWithoutImages;
            initializingImageFilter = false;
        }

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetLifePanel(this);
            RefreshAllSources();
            ReloadLifeList();
        }

        private void RefreshAllSources()
        {
            RefreshReactorSource();
            RefreshNpcSource();
            RefreshMobSource();
        }

        private void LifeModeChanged(object sender, RoutedEventArgs e)
        {
            if (lifeGallery != null)
                ReloadLifeList();
        }

        private void ReloadLifeList()
        {
            thumbnailLoadVersion++;
            thumbnailQueue.Clear();
            queuedThumbnails.Clear();
            loadedThumbnails.Clear();
            thumbnailPumpScheduled = false;

            IEnumerable<LifeEntry> entries = reactorRButton.IsChecked == true
                ? reactors
                : npcRButton.IsChecked == true ? npcs : mobs;

            using (lifeGallery.DeferUpdates())
            {
                lifeGallery.Clear();
                foreach (LifeEntry entry in entries)
                    lifeGallery.Add(placeholderSource, entry.DisplayName, entry);
            }
            lifePreview.Source = null;
        }

        private void LifeGallery_ItemRealized(object sender, AssetGalleryItemEventArgs e)
        {
            AssetGalleryItem item = e.Item;
            if (item?.Tag is not LifeEntry || loadedThumbnails.Contains(item) || !queuedThumbnails.Add(item))
                return;

            thumbnailQueue.Enqueue((item, thumbnailLoadVersion));
            ScheduleThumbnailPump();
        }

        private void ScheduleThumbnailPump()
        {
            if (thumbnailPumpScheduled || thumbnailQueue.Count == 0)
                return;

            thumbnailPumpScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ProcessNextThumbnail));
        }

        private void ProcessNextThumbnail()
        {
            thumbnailPumpScheduled = false;
            while (thumbnailQueue.Count > 0)
            {
                (AssetGalleryItem item, int version) = thumbnailQueue.Dequeue();
                queuedThumbnails.Remove(item);
                if (version != thumbnailLoadVersion || item.Tag is not LifeEntry entry ||
                    !lifeGallery.IsRealized(item))
                {
                    continue;
                }

                bool hasImage = TryLoadThumbnail(entry, out BitmapSource thumbnail);
                if (version != thumbnailLoadVersion)
                    return;

                loadedThumbnails.Add(item);
                if (!hasImage && ApplicationSettings.HideLifeEntriesWithoutImages)
                    lifeGallery.Remove(item);
                else
                    item.Image = thumbnail;
                break;
            }

            ScheduleThumbnailPump();
        }

        private bool TryLoadThumbnail(LifeEntry entry, out BitmapSource thumbnail)
        {
            try
            {
                MapleExtractableInfo info = entry.Type switch
                {
                    LifeEntryType.Reactor => Program.InfoManager.GetReactor(entry.Id),
                    LifeEntryType.Npc => NpcInfo.Get(entry.Id),
                    LifeEntryType.Mob => MobInfo.Get(entry.Id),
                    _ => null
                };

                Bitmap image = info?.Image;
                bool hasImage = image != null && info.Width > 1 && info.Height > 1 &&
                    !BitmapsMatch(image, placeholderBitmap);
                thumbnail = hasImage ? ConvertBitmap(image) : placeholderSource;
                return hasImage;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to load life thumbnail {entry.Id}: {exception.Message}");
                thumbnail = placeholderSource;
                return false;
            }
        }

        private static bool BitmapsMatch(Bitmap left, Bitmap right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Width != right.Width || left.Height != right.Height)
                return false;

            for (int y = 0; y < left.Height; y++)
            {
                for (int x = 0; x < left.Width; x++)
                {
                    if (left.GetPixel(x, y).ToArgb() != right.GetPixel(x, y).ToArgb())
                        return false;
                }
            }
            return true;
        }

        private void ImageFilterChanged(object sender, RoutedEventArgs e)
        {
            if (initializingImageFilter)
                return;

            ApplicationSettings.HideLifeEntriesWithoutImages = hideEntriesWithoutImagesCheckBox.IsChecked == true;
            Program.SettingsManager?.SaveSettings();
            if (hcsm != null)
                ReloadLifeList();
        }

        private void LifeGallery_SelectionChanged(object sender, AssetGalleryItemEventArgs e)
        {
            if (hcsm?.MultiBoard.SelectedBoard == null || e.Item.Tag is not LifeEntry entry)
                return;

            lock (hcsm.MultiBoard)
            {
                switch (entry.Type)
                {
                    case LifeEntryType.Reactor:
                        ReactorInfo reactorInfo = Program.InfoManager.GetReactor(entry.Id);
                        if (reactorInfo == null)
                            return;
                        lifePreview.Source = ConvertBitmap(reactorInfo.Image);
                        hcsm.EnterEditMode(ItemTypes.Reactors);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(reactorInfo);
                        break;

                    case LifeEntryType.Npc:
                        NpcInfo npcInfo = NpcInfo.Get(entry.Id);
                        if (npcInfo == null)
                            return;
                        if (npcInfo.Height == 1 && npcInfo.Width == 1)
                            npcInfo.Image = global::HaCreator.Properties.Resources.placeholder;
                        lifePreview.Source = ConvertBitmap(npcInfo.Image);
                        hcsm.EnterEditMode(ItemTypes.NPCs);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(npcInfo);
                        break;

                    case LifeEntryType.Mob:
                        MobInfo mobInfo = MobInfo.Get(entry.Id);
                        if (mobInfo == null)
                            return;
                        lifePreview.Source = ConvertBitmap(mobInfo.Image);
                        hcsm.EnterEditMode(ItemTypes.Mobs);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(mobInfo);
                        break;
                }

                hcsm.MultiBoard.Focus();
            }
        }

        public void SubscribeToHotSwap(HotSwapRefreshService refreshService)
        {
            if (hotSwapService != null)
                hotSwapService.LifeDataChanged -= OnLifeDataChanged;

            hotSwapService = refreshService;
            if (hotSwapService != null)
                hotSwapService.LifeDataChanged += OnLifeDataChanged;
        }

        private void OnLifeDataChanged(object sender, LifeDataChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => HandleLifeDataChange(e));
                return;
            }
            HandleLifeDataChange(e);
        }

        private void HandleLifeDataChange(LifeDataChangedEventArgs e)
        {
            switch (e.LifeType)
            {
                case Wz.LifeType.Mob:
                    RefreshMobList();
                    break;
                case Wz.LifeType.Npc:
                    RefreshNpcList();
                    break;
                case Wz.LifeType.Reactor:
                    RefreshReactorList();
                    break;
            }
        }

        public void RefreshMobList()
        {
            RefreshMobSource();
            if (mobRButton.IsChecked == true)
                ReloadLifeList();
        }

        public void RefreshNpcList()
        {
            RefreshNpcSource();
            if (npcRButton.IsChecked == true)
                ReloadLifeList();
        }

        public void RefreshReactorList()
        {
            RefreshReactorSource();
            if (reactorRButton.IsChecked == true)
                ReloadLifeList();
        }

        private void RefreshMobSource()
        {
            Program.InfoManager.EnsureMobStringData();
            mobs.Clear();
            mobs.AddRange(Program.InfoManager.GetMobIds()
                .Select(id => new LifeEntry(
                    id,
                    Program.InfoManager.MobNameCache.TryGetValue(id, out string name)
                        ? name
                        : string.Empty,
                    LifeEntryType.Mob))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase));
        }

        private void RefreshNpcSource()
        {
            Program.InfoManager.EnsureNpcStringData();
            npcs.Clear();
            npcs.AddRange(Program.InfoManager.GetNpcIds()
                .Select(id =>
                {
                    if (Program.InfoManager.NpcNameCache.TryGetValue(id, out var info))
                    {
                        string description = string.IsNullOrEmpty(info.Item2) ? string.Empty : $" ({info.Item2})";
                        return new LifeEntry(id, $"{info.Item1}{description}", LifeEntryType.Npc);
                    }
                    return new LifeEntry(id, string.Empty, LifeEntryType.Npc);
                })
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase));
        }

        private void RefreshReactorSource()
        {
            reactors.Clear();
            reactors.AddRange(Program.InfoManager.GetReactorIds()
                .Select(id =>
                {
                    // IMG-directory startup only indexes IDs. Resolve metadata here so
                    // every reactor can be found by name before its thumbnail is realized.
                    string name = Program.InfoManager.GetReactor(id)?.Name;
                    return new LifeEntry(id, name, LifeEntryType.Reactor);
                })
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase));
        }

        private static BitmapSource ConvertBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

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
