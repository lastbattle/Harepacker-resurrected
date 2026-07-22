using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Resources;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class ObjPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private HotSwapRefreshService hotSwapService;
        private readonly ResourceManager resourceManager;

        public ObjPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            resourceManager = new ResourceManager(GetType().Namespace + "." + GetType().Name, GetType().Assembly);
            button_addImage.Content = GetText("Button_AddImage", EditorPanelLocalizer.Text("Add custom object…"));
        }

        public ResourceManager ResourceManager => resourceManager;

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetObjPanel(this);
            objSetListBox.ItemsSource = Program.InfoManager.ObjectSets.Keys.OrderBy(key => key).ToList();
        }

        private string GetText(string key, string fallback) => resourceManager.GetString(key) ?? fallback;

        private void ObjSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            objL0ListBox.ItemsSource = null;
            objL1ListBox.ItemsSource = null;
            objImagesContainer.Clear();
            if (objSetListBox.SelectedItem is not string setName)
                return;
            WzImage image = Program.InfoManager.GetObjectSet(setName);
            if (image == null)
                return;
            objL0ListBox.ItemsSource = image.WzProperties.Select(property => property.Name).ToList();
            if (objL0ListBox.Items.Count > 0) objL0ListBox.SelectedIndex = 0;
        }

        private void ObjL0ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            objL1ListBox.ItemsSource = null;
            objImagesContainer.Clear();
            if (objSetListBox.SelectedItem is not string setName || objL0ListBox.SelectedItem is not string l0)
                return;
            WzImageProperty property = Program.InfoManager.GetObjectSet(setName)?[l0];
            if (property == null)
                return;
            objL1ListBox.ItemsSource = property.WzProperties.Select(child => child.Name).ToList();
            if (objL1ListBox.Items.Count > 0) objL1ListBox.SelectedIndex = 0;
        }

        private void ObjL1ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSelectedCategory();
        }

        private void LoadSelectedCategory()
        {
            button_addImage.IsEnabled = false;
            if (hcsm == null || objSetListBox.SelectedItem is not string setName ||
                objL0ListBox.SelectedItem is not string l0 || objL1ListBox.SelectedItem is not string l1)
            {
                objImagesContainer.Clear();
                return;
            }

            lock (hcsm.MultiBoard)
            {
                WzImageProperty property = Program.InfoManager.GetObjectSet(setName)?[l0]?[l1];
                if (property == null)
                {
                    objImagesContainer.Clear();
                    return;
                }
                using (objImagesContainer.DeferUpdates())
                {
                    objImagesContainer.Clear();
                    foreach (WzSubProperty l2 in property.WzProperties.OfType<WzSubProperty>())
                    {
                        objImagesContainer.AddLazy(l2.Name, () =>
                        {
                            try
                            {
                                ObjectInfo info = ObjectInfo.Get(setName, l0, l1, l2.Name);
                                return (info.Image, (object)info);
                            }
                            catch (InvalidCastException)
                            {
                                return (null, null);
                            }
                        });
                    }
                }
            }
            button_addImage.IsEnabled = true;
        }

        public void OnL1Changed(string l1)
        {
            if (Equals(objL1ListBox.SelectedItem, l1))
                LoadSelectedCategory();
        }

        private void ObjImagesContainer_ItemActivated(object sender, AssetGalleryItemEventArgs e)
        {
            if (hcsm?.MultiBoard.SelectedBoard == null || e.Item.Tag is not ObjectInfo info)
                return;
            lock (hcsm.MultiBoard)
            {
                if (!hcsm.MultiBoard.AssertLayerSelected()) return;
                hcsm.EnterEditMode(ItemTypes.Objects);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                hcsm.MultiBoard.Focus();
            }
        }

        private void ButtonAddImage_Click(object sender, RoutedEventArgs e)
        {
            if (objSetListBox.SelectedItem is not string setName ||
                objL0ListBox.SelectedItem is not string l0 || objL1ListBox.SelectedItem is not string l1)
            {
                MessageBox.Show(GetText("SelectAnImageBefore", EditorPanelLocalizer.Text("Prompt_SelectObjectCategory")),
                    GetText("SelectAnImageBeforeTitle", EditorPanelLocalizer.Text("Title_NoCategorySelected")), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenFileDialog dialog = new()
            {
                Filter = EditorPanelLocalizer.Text("Dialog_ImageOpenFilter"),
                Title = GetText("SelectAnImageToAdd", EditorPanelLocalizer.Text("Dialog_SelectImage"))
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                using Bitmap source = new(dialog.FileName);
                Bitmap newImage = new(source);
                WzImageProperty l1Property = Program.InfoManager.GetObjectSet(setName)?[l0]?[l1];
                if (l1Property == null) return;
                string l2Name = GenerateUniqueObjectName(l1Property);
                WzSubProperty newL2 = new(l2Name);
                newL2["z"] = new WzIntProperty("z", 0);
                WzCanvasProperty canvas = new("0") { PngProperty = new WzPngProperty() };
                canvas.PngProperty.PNG = newImage;
                newL2["0"] = canvas;
                l1Property.WzProperties.Add(newL2);

                System.Drawing.Point origin = new(newImage.Width / 2, newImage.Height);
                ObjectInfo info = new(newImage, origin, setName, l0, l1, l2Name, newL2);
                objImagesContainer.Add(newImage, l2Name, info);
                MarkUpdated(l1Property);
                MessageBox.Show(string.Format(GetText("ImageAddSuccessful", EditorPanelLocalizer.Text("Message_ObjectAdded")), dialog.FileName, l2Name),
                    GetText("ImageAddSuccessfulTitle", EditorPanelLocalizer.Text("Title_ObjectAdded")));
            }
            catch (Exception exception)
            {
                MessageBox.Show(EditorPanelLocalizer.Format("Error_AddingImage", exception.Message), EditorPanelLocalizer.Text("Common_Error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GenerateUniqueObjectName(WzImageProperty l1Property)
        {
            int counter = 1;
            while (l1Property.WzProperties.Any(property => property.Name == counter.ToString())) counter++;
            return counter.ToString();
        }

        private async void UpscaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (objImagesContainer.SelectedItem?.Tag is not ObjectInfo info) return;
            WzImageProperty property = Program.InfoManager.GetObjectSet(info.oS)?[info.l0]?[info.l1]?[info.l2];
            if (property is not WzSubProperty sub || sub["0"] is not WzCanvasProperty canvas) return;
            using Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
            UpscaleImageForm form = new(bitmap);
            form.ShowDialog();
            if (!form.UserAcceptedImage) return;
            canvas.PngProperty.PNG = form.UpscaledImage;
            info.Image = form.UpscaledImage;
            MarkUpdated(property);
            LoadSelectedCategory();
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            if (objImagesContainer.SelectedItem?.Tag is not ObjectInfo info || info.Image == null) return;
            SaveFileDialog dialog = new()
            {
                FileName = $"{info.oS}.{info.l0}.{info.l1}",
                Title = EditorPanelLocalizer.Text("Dialog_SaveObjectImage", "Save object image"),
                Filter = EditorPanelLocalizer.Text("Dialog_ImageSaveFilterShort")
            };
            if (dialog.ShowDialog() != true) return;
            ImageFormat format = dialog.FilterIndex switch
            {
                2 => ImageFormat.Gif,
                3 => ImageFormat.Bmp,
                4 => ImageFormat.Jpeg,
                5 => ImageFormat.Tiff,
                _ => ImageFormat.Png
            };
            info.Image.Save(dialog.FileName, format);
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (objImagesContainer.SelectedItem?.Tag is not ObjectInfo info) return;
            if (MessageBox.Show(GetText("ConfirmItemDelete", EditorPanelLocalizer.Text("Confirm_DeleteObject")),
                GetText("ConfirmItemDeleteTitle", EditorPanelLocalizer.Text("Title_DeleteObject")), MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            WzImageProperty l1 = Program.InfoManager.GetObjectSet(info.oS)?[info.l0]?[info.l1];
            WzImageProperty l2 = l1?[info.l2];
            if (l1 == null || l2 == null) return;
            l1.WzProperties.Remove(l2);
            objImagesContainer.Remove(objImagesContainer.SelectedItem);
            MarkUpdated(l1);
        }

        private static void MarkUpdated(WzImageProperty property)
        {
            WzObject directory = property.GetTopMostWzDirectory();
            WzObject image = property.GetTopMostWzImage();
            Program.WzManager.SetWzFileUpdated(directory.Name, image as WzImage);
        }

        public void SubscribeToHotSwap(HotSwapRefreshService refreshService)
        {
            if (hotSwapService != null) hotSwapService.ObjectSetChanged -= OnObjectSetChanged;
            hotSwapService = refreshService;
            if (hotSwapService != null) hotSwapService.ObjectSetChanged += OnObjectSetChanged;
        }

        private void OnObjectSetChanged(object sender, ObjectSetChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => HandleObjectSetChange(e));
                return;
            }
            HandleObjectSetChange(e);
        }

        private void HandleObjectSetChange(ObjectSetChangedEventArgs e)
        {
            string selected = objSetListBox.SelectedItem as string;
            objSetListBox.ItemsSource = Program.InfoManager.ObjectSets.Keys.OrderBy(key => key).ToList();
            if (e.ChangeType == AssetChangeType.Removed && selected == e.SetName)
                ClearObjectDisplay();
            else if (e.ChangeType == AssetChangeType.Modified && selected == e.SetName)
                RefreshCurrentObjectSet();
            else if (selected != null && objSetListBox.Items.Contains(selected))
                objSetListBox.SelectedItem = selected;
        }

        public void RefreshCurrentObjectSet()
        {
            if (objSetListBox.SelectedItem is not string selected) return;
            Program.InfoManager.RefreshObjectSet(selected);
            ObjSetListBox_SelectionChanged(this, null);
        }

        private void ClearObjectDisplay()
        {
            objL0ListBox.ItemsSource = null;
            objL1ListBox.ItemsSource = null;
            objImagesContainer.Clear();
            button_addImage.IsEnabled = false;
        }
    }
}
