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
        private readonly BitmapSource placeholderSource;

        public LifePanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            placeholderSource = ConvertBitmap(global::HaCreator.Properties.Resources.placeholder);
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
            lifeGallery.Clear();

            IEnumerable<LifeEntry> entries = reactorRButton.IsChecked == true
                ? reactors
                : npcRButton.IsChecked == true ? npcs : mobs;

            foreach (LifeEntry entry in entries.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
                lifeGallery.Add(placeholderSource, entry.DisplayName, entry);
            lifePreview.Source = null;
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
