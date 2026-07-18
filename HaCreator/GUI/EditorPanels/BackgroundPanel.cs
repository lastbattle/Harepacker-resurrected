using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using HaSharedLibrary.GUI;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class BackgroundPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private HotSwapRefreshService _hotSwapService;
        private readonly ContextMenu _imageContextMenu = new();
        private readonly ContextMenu _spineContextMenu = new();
        private AssetGalleryItem _contextItem;
        private ResourceManager resourceMan;

        public BackgroundPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            BuildContextMenus();
            ApplyLocalizedText();
            backgroundGallery.ItemActivated += BackgroundGallery_ItemActivated;
            backgroundGallery.ContextRequested += BackgroundGallery_ContextRequested;
        }

        public ResourceManager ResourceManager
        {
            get
            {
                resourceMan ??= new ResourceManager(GetType().Namespace + "." + GetType().Name, GetType().Assembly);
                return resourceMan;
            }
        }

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetBackgroundPanel(this);

            string selected = bgSetListBox.SelectedItem as string;
            bgSetListBox.Items.Clear();
            foreach (string setName in Program.InfoManager.BackgroundSets.Keys.OrderBy(key => key))
                bgSetListBox.Items.Add(setName);
            if (selected != null && bgSetListBox.Items.Contains(selected))
                bgSetListBox.SelectedItem = selected;
        }

        private void ApplyLocalizedText()
        {
            addImageButton.Content = ResourceManager.GetString("Button_AddImage") ?? EditorPanelLocalizer.Text("Add custom background…");
        }

        private void BuildContextMenus()
        {
            _imageContextMenu.Items.Add(CreateMenuItem(ResourceManager.GetString("ContextStripMenu_Save") ?? EditorPanelLocalizer.Text("Save"), SaveItem_Click));
            _imageContextMenu.Items.Add(CreateMenuItem(ResourceManager.GetString("ContextStripMenu_AIUpscale") ?? EditorPanelLocalizer.Text("AI upscale…"), AiUpscaleItem_Click));
            _imageContextMenu.Items.Add(new Separator());
            _imageContextMenu.Items.Add(CreateMenuItem(ResourceManager.GetString("ContextStripMenu_Delete") ?? EditorPanelLocalizer.Text("Menu_Delete", "Delete"), DeleteItem_Click));

            _spineContextMenu.Items.Add(CreateMenuItem(ResourceManager.GetString("ContextStripMenu_Preview") ?? EditorPanelLocalizer.Text("Preview"), PreviewItem_Click));
            _spineContextMenu.Items.Add(new Separator());
            _spineContextMenu.Items.Add(CreateMenuItem(ResourceManager.GetString("ContextStripMenu_Delete") ?? EditorPanelLocalizer.Text("Menu_Delete", "Delete"), DeleteItem_Click));
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            MenuItem item = new() { Header = header };
            item.Click += handler;
            return item;
        }

        private BackgroundInfoType SelectedBackgroundType()
        {
            if (spineRadioButton.IsChecked == true)
                return BackgroundInfoType.Spine;
            if (animatedRadioButton.IsChecked == true)
                return BackgroundInfoType.Animation;
            return BackgroundInfoType.Background;
        }

        private void BackgroundSet_SelectionChanged(object sender, RoutedEventArgs e)
        {
            LoadSelectedBackgroundSet();
        }

        private void BackgroundType_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                LoadSelectedBackgroundSet();
        }

        private void LoadSelectedBackgroundSet()
        {
            backgroundGallery.Clear();
            if (hcsm == null || bgSetListBox.SelectedItem is not string setName)
                return;

            BackgroundInfoType infoType = SelectedBackgroundType();
            WzImage setImage = Program.InfoManager.GetBackgroundSet(setName);
            WzImageProperty parent = setImage?[infoType.ToPropertyString()];
            if (parent?.WzProperties == null)
                return;

            foreach (WzImageProperty property in parent.WzProperties)
            {
                BackgroundInfo info = BackgroundInfo.Get(hcsm.MultiBoard.GraphicsDevice, setName, infoType, property.Name);
                if (info != null)
                    backgroundGallery.Add(info.Image, property.Name, info);
            }
        }

        private void BackgroundGallery_ItemActivated(object sender, AssetGalleryItemEventArgs e)
        {
            if (hcsm == null || e.Item?.Tag is not BackgroundInfo info)
                return;

            lock (hcsm.MultiBoard)
            {
                hcsm.EnterEditMode(ItemTypes.Backgrounds);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                hcsm.MultiBoard.Focus();
            }
        }

        private void BackgroundGallery_ContextRequested(object sender, AssetGalleryItemEventArgs e)
        {
            if (e.Item?.Tag is not BackgroundInfo info)
                return;

            _contextItem = e.Item;
            ContextMenu menu = info.Type == BackgroundInfoType.Spine ? _spineContextMenu : _imageContextMenu;
            menu.PlacementTarget = backgroundGallery;
            menu.IsOpen = true;
        }

        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (bgSetListBox.SelectedItem is not string setName)
                return;

            using System.Windows.Forms.OpenFileDialog dialog = new()
            {
                Filter = EditorPanelLocalizer.Text("Dialog_ImageOpenFilter"),
                Title = ResourceManager.GetString("SelectAnImageToAdd") ?? EditorPanelLocalizer.Text("Dialog_SelectImage")
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                Bitmap image = new(dialog.FileName);
                BackgroundInfoType infoType = BackgroundInfoType.Background;
                WzImage setImage = Program.InfoManager.GetBackgroundSet(setName);
                if (setImage?[infoType.ToPropertyString()] is not WzSubProperty parent)
                    return;

                string name = GenerateUniqueBgName(setName, infoType.ToPropertyString());
                WzCanvasProperty property = new(name) { PngProperty = new WzPngProperty() };
                property.PngProperty.PNG = image;
                property.AddProperty(new WzIntProperty("z", 0));
                property.AddProperty(new WzVectorProperty("origin", 0, 0));
                parent.AddProperty(property);

                System.Drawing.Point origin = new(image.Width / 2, image.Height);
                BackgroundInfo info = new(property, image, origin, setName, infoType, name, property, null);
                backgroundGallery.Add(info.Image, name, info);
                Program.WzManager.SetWzFileUpdated(setImage.WzFileParent.Name, setImage);

                System.Windows.MessageBox.Show(
                    string.Format(ResourceManager.GetString("ImageAddSuccessful") ?? EditorPanelLocalizer.Text("Message_BackgroundAdded"), dialog.FileName, name),
                    ResourceManager.GetString("ImageAddSuccessfulTitle") ?? EditorPanelLocalizer.Text("Title_ImageAdded"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(EditorPanelLocalizer.Format("Error_AddingImage", exception.Message), EditorPanelLocalizer.Text("Common_Error", "Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateUniqueBgName(string setName, string typeName)
        {
            int counter = 1;
            WzImageProperty parent = Program.InfoManager.GetBackgroundSet(setName)?[typeName];
            if (parent?.WzProperties == null)
                return counter.ToString();
            while (parent.WzProperties.Any(property => property.Name == counter.ToString()))
                counter++;
            return counter.ToString();
        }

        private void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextItem?.Tag is not BackgroundInfo info || info.Image == null ||
                bgSetListBox.SelectedItem is not string setName || info.Type == BackgroundInfoType.Spine)
                return;

            using System.Windows.Forms.SaveFileDialog dialog = new()
            {
                FileName = $"{setName}.{info.Type.ToPropertyString()}.{info.no}",
                Title = EditorPanelLocalizer.Text("Dialog_SaveImage", "Select where to save the image..."),
                Filter = EditorPanelLocalizer.Text("Dialog_ImageSaveFilterLong")
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            System.Drawing.Imaging.ImageFormat format = dialog.FilterIndex switch
            {
                2 => System.Drawing.Imaging.ImageFormat.Gif,
                3 => System.Drawing.Imaging.ImageFormat.Bmp,
                4 => System.Drawing.Imaging.ImageFormat.Jpeg,
                5 => System.Drawing.Imaging.ImageFormat.Tiff,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };
            info.Image.Save(dialog.FileName, format);
        }

        private void AiUpscaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextItem?.Tag is not BackgroundInfo info || bgSetListBox.SelectedItem is not string setName)
                return;

            WzImageProperty parent = Program.InfoManager.GetBackgroundSet(setName)?[SelectedBackgroundType().ToPropertyString()];
            if (parent is not WzSubProperty parentProperty || parentProperty[info.no] is not WzCanvasProperty canvas)
                return;

            Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
            UpscaleImageForm dialog = new(bitmap) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            if (!dialog.UserAcceptedImage)
                return;

            Bitmap upscaled = dialog.UpscaledImage;
            canvas.PngProperty.PNG = upscaled;
            info.Image = upscaled;
            backgroundGallery.UpdateImage(_contextItem, upscaled);
            WzObject topDirectory = parent.GetTopMostWzDirectory();
            Program.WzManager.SetWzFileUpdated(topDirectory.Name, parent.Parent as WzImage);
        }

        private void PreviewItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextItem?.Tag is not BackgroundInfo info)
                return;

            WzImageProperty atlas = info.WzImageProperty.WzProperties.FirstOrDefault(property =>
                property is WzStringProperty stringProperty && stringProperty.IsSpineAtlasResources);
            if (atlas is not WzStringProperty stringObject)
                return;

            Thread thread = new(() =>
            {
                WzSpineAnimationItem item = new(stringObject);
                SpineAnimationWindow window = new(item, stringObject.Parent?.FullPath ?? "Animate");
                window.Run();
            });
            thread.Start();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextItem?.Tag is not BackgroundInfo info || bgSetListBox.SelectedItem is not string setName)
                return;

            MessageBoxResult result = System.Windows.MessageBox.Show(
                ResourceManager.GetString("ConfirmItemDelete") ?? EditorPanelLocalizer.Text("Confirm_DeleteBackground"),
                ResourceManager.GetString("ConfirmItemDeleteTitle") ?? EditorPanelLocalizer.Text("Title_ConfirmDelete"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            WzImageProperty parent = Program.InfoManager.GetBackgroundSet(setName)?[SelectedBackgroundType().ToPropertyString()];
            WzImageProperty property = parent?[info.no];
            if (property == null || !parent.WzProperties.Contains(property))
                return;

            parent.WzProperties.Remove(property);
            backgroundGallery.Remove(_contextItem);
            WzObject topDirectory = parent.GetTopMostWzDirectory();
            Program.WzManager.SetWzFileUpdated(topDirectory.Name, parent.Parent as WzImage);
            _contextItem = null;
        }

        public void SubscribeToHotSwap(HotSwapRefreshService refreshService)
        {
            if (_hotSwapService != null)
                _hotSwapService.BackgroundSetChanged -= OnBackgroundSetChanged;
            _hotSwapService = refreshService;
            if (_hotSwapService != null)
                _hotSwapService.BackgroundSetChanged += OnBackgroundSetChanged;
        }

        private void OnBackgroundSetChanged(object sender, BackgroundSetChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => HandleBackgroundSetChange(e)));
                return;
            }
            HandleBackgroundSetChange(e);
        }

        private void HandleBackgroundSetChange(BackgroundSetChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case AssetChangeType.Added:
                    AddSetIfMissing(e.SetName);
                    break;
                case AssetChangeType.Removed:
                    bool wasSelected = Equals(bgSetListBox.SelectedItem, e.SetName);
                    bgSetListBox.Items.Remove(e.SetName);
                    if (wasSelected)
                    {
                        backgroundGallery.Clear();
                        if (bgSetListBox.Items.Count > 0)
                            bgSetListBox.SelectedIndex = 0;
                    }
                    break;
                case AssetChangeType.Modified:
                    AddSetIfMissing(e.SetName);
                    if (Equals(bgSetListBox.SelectedItem, e.SetName))
                        RefreshCurrentBackgroundSet();
                    break;
            }
        }

        private void AddSetIfMissing(string setName)
        {
            if (!bgSetListBox.Items.Contains(setName))
            {
                bgSetListBox.Items.Add(setName);
                SortBackgroundSetList();
            }
        }

        public void RefreshCurrentBackgroundSet()
        {
            if (bgSetListBox.SelectedItem is not string selectedSet)
                return;
            Program.InfoManager.RefreshBackgroundSet(selectedSet);
            LoadSelectedBackgroundSet();
        }

        private void SortBackgroundSetList()
        {
            List<string> items = bgSetListBox.Items.Cast<string>().OrderBy(item => item).ToList();
            object selected = bgSetListBox.SelectedItem;
            bgSetListBox.Items.Clear();
            foreach (string item in items)
                bgSetListBox.Items.Add(item);
            if (selected != null && bgSetListBox.Items.Contains(selected))
                bgSetListBox.SelectedItem = selected;
        }
    }
}
