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

        private sealed record LifeEntry(string Id, string DisplayName, LifeEntryType Type);

        private readonly List<LifeEntry> reactors = new();
        private readonly List<LifeEntry> npcs = new();
        private readonly List<LifeEntry> mobs = new();

        private HaCreatorStateManager hcsm;
        private HotSwapRefreshService hotSwapService;
        private readonly Bitmap placeholderBitmap;
        private readonly BitmapSource placeholderSource;
        private int thumbnailLoadVersion;
        private bool initializingImageFilter;

        public LifePanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            placeholderBitmap = global::HaCreator.Properties.Resources.placeholder;
            placeholderSource = ConvertBitmap(placeholderBitmap);
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
            int loadVersion = ++thumbnailLoadVersion;
            lifeGallery.Clear();

            IEnumerable<LifeEntry> entries = reactorRButton.IsChecked == true
                ? reactors
                : npcRButton.IsChecked == true ? npcs : mobs;

            List<(LifeEntry Entry, AssetGalleryItem Item)> pendingThumbnails = new();
            foreach (LifeEntry entry in entries.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
                pendingThumbnails.Add((entry, lifeGallery.Add(placeholderSource, entry.DisplayName, entry)));
            lifePreview.Source = null;
            _ = LoadThumbnailsAsync(pendingThumbnails, loadVersion);
        }

        private async System.Threading.Tasks.Task LoadThumbnailsAsync(
            IReadOnlyList<(LifeEntry Entry, AssetGalleryItem Item)> pendingThumbnails, int loadVersion)
        {
            for (int index = 0; index < pendingThumbnails.Count; index++)
            {
                if (loadVersion != thumbnailLoadVersion)
                    return;

                (LifeEntry entry, AssetGalleryItem item) = pendingThumbnails[index];
                bool hasImage = TryLoadThumbnail(entry, out BitmapSource thumbnail);
                if (loadVersion != thumbnailLoadVersion)
                    return;

                if (!hasImage && ApplicationSettings.HideLifeEntriesWithoutImages)
                    lifeGallery.Remove(item);
                else
                    item.Image = thumbnail;

                if ((index + 1) % 8 == 0)
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private bool TryLoadThumbnail(LifeEntry entry, out BitmapSource thumbnail)
        {
            try
            {
                MapleExtractableInfo info = entry.Type switch
                {
                    LifeEntryType.Reactor => Program.InfoManager.Reactors.TryGetValue(entry.Id, out ReactorInfo reactorInfo)
                        ? reactorInfo
                        : null,
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
                        if (!Program.InfoManager.Reactors.TryGetValue(entry.Id, out ReactorInfo reactorInfo))
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
            mobs.Clear();
            foreach (KeyValuePair<string, string> entry in Program.InfoManager.MobNameCache.ToList())
                mobs.Add(new LifeEntry(entry.Key, $"{entry.Key} - {entry.Value}", LifeEntryType.Mob));
        }

        private void RefreshNpcSource()
        {
            npcs.Clear();
            foreach (KeyValuePair<string, Tuple<string, string>> entry in Program.InfoManager.NpcNameCache.ToList())
            {
                string description = string.IsNullOrEmpty(entry.Value.Item2)
                    ? string.Empty
                    : $" ({entry.Value.Item2})";
                npcs.Add(new LifeEntry(
                    entry.Key,
                    $"{entry.Key} - {entry.Value.Item1}{description}",
                    LifeEntryType.Npc));
            }
        }

        private void RefreshReactorSource()
        {
            reactors.Clear();
            foreach (KeyValuePair<string, ReactorInfo> entry in Program.InfoManager.Reactors.ToList())
            {
                string name = string.IsNullOrEmpty(entry.Value.Name)
                    ? entry.Value.ID
                    : $"{entry.Value.ID} ({entry.Value.Name})";
                reactors.Add(new LifeEntry(entry.Key, name, LifeEntryType.Reactor));
            }
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
