/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.CustomControls;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaSharedLibrary.GUI;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Shapes;
using WeifenLuo.WinFormsUI.Docking;
using Xceed.Wpf.AvalonDock.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class BackgroundPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        // ContextMenuStrip
        private readonly ContextMenuStrip contextMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip contextMenu_spine = new ContextMenuStrip();


        public BackgroundPanel()
        {
            InitializeComponent();

            // context menu
            ToolStripMenuItem deleteItem = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_Delete"));
            deleteItem.Click += DeleteItem_Click;

            ToolStripMenuItem aiUpscaleItem = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_AIUpscale"));
            aiUpscaleItem.Click += aiUpscaleItem_Click;

            contextMenu.Items.Add(deleteItem);
            contextMenu.Items.Add(aiUpscaleItem);

            // context menu for spine
            ToolStripMenuItem deleteItem2 = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_Delete"));
            deleteItem2.Click += DeleteItem_Click;

            ToolStripMenuItem previewItem = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_Preview"));
            previewItem.Click += previewItem_Click;

            contextMenu_spine.Items.Add(previewItem);
            contextMenu_spine.Items.Add(deleteItem2);

            // localisation
            button_addImage.Text = this.ResourceManager.GetString("Button_AddImage");
        }

        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;

            List<string> sortedBgSets = new List<string>();
            foreach (KeyValuePair<string, WzImage> bS in Program.InfoManager.BackgroundSets)
            {
                sortedBgSets.Add(bS.Key);
            }
            sortedBgSets.Sort();
            foreach (string bS in sortedBgSets)
            {
                bgSetListBox.Items.Add(bS);
            }
        }

        private BackgroundInfoType GetBackGroundInfoTypeByCheckbox()
        {
            BackgroundInfoType infoType = BackgroundInfoType.Animation;
            if (radioButton_spine.Checked)
            {
                infoType = BackgroundInfoType.Spine;
            }
            else if (aniBg.Checked)
            {
                infoType = BackgroundInfoType.Animation;
            }
            else
            {
                infoType = BackgroundInfoType.Background;
            }
            return infoType;
        }

        /// <summary>
        /// On image selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bgSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bgSetListBox.SelectedItem == null)
                return;
            bgImageContainer.Controls.Clear();

            BackgroundInfoType infoType = GetBackGroundInfoTypeByCheckbox();

            WzImageProperty parentProp = Program.InfoManager.BackgroundSets[(string)bgSetListBox.SelectedItem][infoType.ToPropertyString()];
            if (parentProp == null || parentProp.WzProperties == null)
                return;

            foreach (WzImageProperty prop in parentProp.WzProperties)
            {
                BackgroundInfo bgInfo = BackgroundInfo.Get(hcsm.MultiBoard.GraphicsDevice, (string)bgSetListBox.SelectedItem, infoType, prop.Name);
                if (bgInfo == null)
                    continue;

                ImageViewer item = bgImageContainer.Add(bgInfo.Image, prop.Name, true);
                item.Tag = bgInfo;
                item.MouseDown += new MouseEventHandler(bgItem_Click);
                item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                item.MaxHeight = UserSettings.ImageViewerHeight;
                item.MaxWidth = UserSettings.ImageViewerWidth;
            }
        }

        /// <summary>
        /// On click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bgItem_Click(object sender, MouseEventArgs e)
        {
            ImageViewer imageViewer = sender as ImageViewer;
            BackgroundInfo bgInfo = (BackgroundInfo)((ImageViewer)sender).Tag;

            if (e.Button == MouseButtons.Left)
            {
                lock (hcsm.MultiBoard)
                {
                    hcsm.EnterEditMode(ItemTypes.Backgrounds);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(bgInfo);
                    hcsm.MultiBoard.Focus();
                    ((ImageViewer)sender).IsActive = true;
                }
            }
            else if (e.Button == MouseButtons.Right) // right click
            {
                // Show context menu for delete option
                ShowContextMenu((ImageViewer)sender, e.Location, bgInfo.Type == BackgroundInfoType.Spine);
            }
        }

        /// <summary>
        /// Add image button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addImage_Click(object sender, EventArgs e)
        {
            if (bgSetListBox.SelectedItem == null)
            {
                return;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                //openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
                openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
                openFileDialog.Title = this.ResourceManager.GetString("SelectAnImageToAdd");

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Bitmap newImage = new Bitmap(openFileDialog.FileName); // dont close this

                        string bgSetName = (string)bgSetListBox.SelectedItem;
                        BackgroundInfoType infoType = GetBackGroundInfoTypeByCheckbox();

                        WzImage bgSetImage = Program.InfoManager.BackgroundSets[bgSetName];
                        WzSubProperty parentProp = (WzSubProperty)bgSetImage[infoType.ToPropertyString()];

                        // Generate a new unique name for the background
                        string newBgName = GenerateUniqueBgName(bgSetName, infoType.ToPropertyString());

                        // Create a new WzCanvasProperty for the image
                        WzCanvasProperty newBgProp = new WzCanvasProperty(newBgName);
                        newBgProp.PngProperty = new WzPngProperty();
                        newBgProp.PngProperty.PNG = newImage;

                        // Add the new property to the parent
                        parentProp.AddProperty(newBgProp);

                        // there's no fixed place or consistency in the WZ
                        // sometimes its at the top-center
                        // sometimes bottom-left or bottom-right
                        // it depends on nexon-devs, better off making this automatic for the user.
                        Point point = new Point(newImage.Width / 2, newImage.Height); // set at bottom-center

                        // Create a new BackgroundInfo object
                        BackgroundInfo newBgInfo = new BackgroundInfo(newBgProp, newImage, point, bgSetName, infoType, newBgName, parentProp, null);
                        // BackgroundInfo(WzImageProperty imageProperty, Bitmap image, System.Drawing.Point origin, string bS, BackgroundInfoType _type, string no, WzObject parentObject, WzSpineAnimationItem wzSpineAnimationItem)
                        //BackgroundInfo newBgInfo = BackgroundInfo.Get(hcsm.MultiBoard.GraphicsDevice, bgSetName, infoType, newBgName);


                        // Add the new image to the container
                        ImageViewer newItem = bgImageContainer.Add(newBgInfo.Image, newBgName, true);
                        newItem.Tag = newBgInfo;
                        newItem.MouseDown += new MouseEventHandler(bgItem_Click);
                        newItem.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                        newItem.MaxHeight = UserSettings.ImageViewerHeight;
                        newItem.MaxWidth = UserSettings.ImageViewerWidth;

                        // Flag WZ file as updated
                        Program.WzManager.SetWzFileUpdated(bgSetImage.WzFileParent.Name, bgSetImage);

                        MessageBox.Show(
                            string.Format(this.ResourceManager.GetString("ImageAddSuccessful"), openFileDialog.FileName, newBgName),
                            this.ResourceManager.GetString("ImageAddSuccessfulTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error adding image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Generates a new object name for the image
        /// 0,1,2,3,4,5,6 ...
        /// </summary>
        /// <param name="objSetName"></param>
        /// <param name="l0Name"></param>
        /// <param name="l1Name"></param>
        /// <returns></returns>
        private string GenerateUniqueBgName(string objSetName, string infoTypeName)
        {
            int counter = 1;
            WzImageProperty l1Prop = Program.InfoManager.BackgroundSets[objSetName][infoTypeName];
            while (l1Prop.WzProperties.Any(p => p.Name == counter.ToString()))
            {
                counter++;
            }
            return counter.ToString();
        }

        #region Context Menu
        /// <summary>
        /// Show context menu
        /// </summary>
        /// <param name="item"></param>
        /// <param name="location"></param>
        /// <param name="bIsSpine">Is spine object selected</param>
        private void ShowContextMenu(ImageViewer item, Point location, bool bIsSpine)
        {
            if (bIsSpine)
                contextMenu_spine.Show(item, location);
            else
                contextMenu.Show(item, location);
        }

        /// <summary>
        /// Upscale context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private async void aiUpscaleItem_Click(object sender, EventArgs e)
        {
            ImageViewer selectedItem = contextMenu.SourceControl as ImageViewer;
            if (selectedItem == null)
            {
                return;
            }
            if (bgSetListBox.SelectedItem == null)
                return;

            BackgroundInfo objInfo = (BackgroundInfo)selectedItem.Tag;

            BackgroundInfoType infoType = GetBackGroundInfoTypeByCheckbox();

            WzImageProperty parentProp = Program.InfoManager.BackgroundSets[(string)bgSetListBox.SelectedItem]?[infoType.ToPropertyString()]; // i,e syarenian.img  dragonDream.img > "back"
            if (parentProp != null)
            {
                WzSubProperty parentSubProp = (WzSubProperty)parentProp;
                WzCanvasProperty l2SubImg = (WzCanvasProperty)parentSubProp[objInfo.no];
                Bitmap bitmap = l2SubImg.GetLinkedWzCanvasBitmap();

                UpscaleImageForm upscaleForm = new UpscaleImageForm(bitmap);
                upscaleForm.ShowDialog();
                if (upscaleForm.UserAcceptedImage)
                {
                    Bitmap upscaledBitmap = upscaleForm.UpscaledImage;
                    l2SubImg.PngProperty.PNG = upscaledBitmap;
                    objInfo.Image = upscaledBitmap;
                    selectedItem.Image = upscaledBitmap;

                    // flag WZ files changed to save it
                    WzObject topMostWzDir = parentProp.GetTopMostWzDirectory();
                    WzImage topMostWzImg = parentProp.Parent as WzImage;

                    Program.WzManager.SetWzFileUpdated(topMostWzDir.Name, topMostWzImg as WzImage);
                }
            }
        }

        /// <summary>
        /// Event handler for the preview menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void previewItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem_ = sender as ToolStripMenuItem;
            BackgroundInfo bgInfo_ = menuItem_.Tag as BackgroundInfo;

            WzImageProperty spineAtlasProp = bgInfo_.WzImageProperty.WzProperties.FirstOrDefault(
                wzprop => wzprop is WzStringProperty && ((WzStringProperty)wzprop).IsSpineAtlasResources);

            if (spineAtlasProp != null)
            {
                WzStringProperty stringObj = (WzStringProperty)spineAtlasProp;
                Thread thread = new Thread(() =>
                {
                    WzSpineAnimationItem item = new WzSpineAnimationItem(stringObj);

                    string path_title = stringObj.Parent?.FullPath ?? "Animate";

                    // Create xna window
                    SpineAnimationWindow Window = new SpineAnimationWindow(item, path_title);
                    Window.Run();
                });
                thread.Start();
                thread.Join();
            }
        }

        /// <summary>
        /// Event handler for the Delete menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteItem_Click(object sender, EventArgs e)
        {
            ImageViewer selectedItem = contextMenu.SourceControl as ImageViewer;
            if (selectedItem != null)
            {
                // Show confirmation dialog
                DialogResult result = MessageBox.Show(
                    this.ResourceManager.GetString("ConfirmItemDelete"),
                    this.ResourceManager.GetString("ConfirmItemDeleteTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    BackgroundInfoType infoType = GetBackGroundInfoTypeByCheckbox();

                    // delete off cached obj
                    BackgroundInfo objInfo = (BackgroundInfo)selectedItem.Tag;

                    WzImageProperty parentProp = Program.InfoManager.BackgroundSets[(string)bgSetListBox.SelectedItem]?[infoType.ToPropertyString()];
                    if (parentProp != null)
                    {
                        WzImageProperty removeL2Prop = parentProp[objInfo.no];

                        if (parentProp.WzProperties.Contains(removeL2Prop))
                        {
                            parentProp.WzProperties.Remove(removeL2Prop);

                            // Perform delete operation
                            //objImagesContainer.Remove(selectedItem);
                            selectedItem.Dispose();

                            // flag WZ files changed to save it
                            WzObject topMostWzDir = parentProp.GetTopMostWzDirectory();
                            WzImage topMostWzImg = parentProp.Parent as WzImage;

                            Program.WzManager.SetWzFileUpdated(topMostWzDir.Name, topMostWzImg as WzImage);
                        }
                    }
                }
            }
        }
        #endregion
    }
}
