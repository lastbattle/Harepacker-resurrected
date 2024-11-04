/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.CustomControls;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class ObjPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        // ContextMenuStrip for the 'obj'
        private readonly ContextMenuStrip contextMenu = new ContextMenuStrip();

        /// <summary>
        /// Constructor
        /// </summary>
        public ObjPanel()
        {
            InitializeComponent();

            // context menu
            ToolStripMenuItem saveItem = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_Save"));
            saveItem.Click += saveItem_Click;

            ToolStripMenuItem deleteItem = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_Delete"));
            deleteItem.Click += DeleteItem_Click;

            ToolStripMenuItem aiUpscaleItem = new ToolStripMenuItem(this.ResourceManager.GetString("ContextStripMenu_AIUpscale"));
            aiUpscaleItem.Click += aiUpscaleItem_Click;

            contextMenu.Items.Add(saveItem);
            contextMenu.Items.Add(deleteItem);
            contextMenu.Items.Add(aiUpscaleItem);

            // localisation
            button_addImage.Text = this.ResourceManager.GetString("Button_AddImage");
        }

        /// <summary>
        /// Init
        /// </summary>
        /// <param name="hcsm"></param>
        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            hcsm.SetObjPanel(this);

            List<string> sortedObjSets = new List<string>();
            foreach (KeyValuePair<string, WzImage> oS in Program.InfoManager.ObjectSets)
            {
                sortedObjSets.Add(oS.Key);
            }
            sortedObjSets.Sort();
            foreach (string oS in sortedObjSets)
            {
                objSetListBox.Items.Add(oS);
            }
        }

        /// <summary>
        /// On obj selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void objSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (objSetListBox.SelectedItem == null)
                return;

            objL0ListBox.Items.Clear();
            objL1ListBox.Items.Clear();
            objImagesContainer.Controls.Clear();
            WzImage oSImage = Program.InfoManager.ObjectSets[(string)objSetListBox.SelectedItem];
            if (!oSImage.Parsed)
            {
                oSImage.ParseImage();
            }
            foreach (WzImageProperty l0Prop in oSImage.WzProperties)
            {
                objL0ListBox.Items.Add(l0Prop.Name);
            }
            // select the first item automatically
            if (objL0ListBox.Items.Count > 0)
            {
                objL0ListBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// On L0 selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void objL0ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (objL0ListBox.SelectedItem == null)
                return;

            objL1ListBox.Items.Clear();
            objImagesContainer.Controls.Clear();
            WzImageProperty l0Prop = Program.InfoManager.ObjectSets[(string)objSetListBox.SelectedItem][(string)objL0ListBox.SelectedItem];
            foreach (WzImageProperty l1Prop in l0Prop.WzProperties)
            {
                objL1ListBox.Items.Add(l1Prop.Name);
            }
            // select the first item automatically
            if (objL1ListBox.Items.Count > 0)
            {
                objL1ListBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// On L1 selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void objL1ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                if (objL1ListBox.SelectedItem == null)
                    return;

                objImagesContainer.Controls.Clear();
                WzImageProperty l1Prop = Program.InfoManager.ObjectSets[(string)objSetListBox.SelectedItem][(string)objL0ListBox.SelectedItem][(string)objL1ListBox.SelectedItem];

                foreach (WzSubProperty l2Prop in l1Prop.WzProperties)
                {
                    try
                    {
                        ObjectInfo info = ObjectInfo.Get((string)objSetListBox.SelectedItem, (string)objL0ListBox.SelectedItem, (string)objL1ListBox.SelectedItem, l2Prop.Name);
                        Bitmap image = info.Image;
                        ImageViewer item = objImagesContainer.Add(image, l2Prop.Name, true);

                        item.Tag = info;
                        item.MouseDown += new MouseEventHandler(objItem_Click);
                        item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                        item.MaxHeight = UserSettings.ImageViewerHeight;
                        item.MaxWidth = UserSettings.ImageViewerWidth;
                    }
                    catch (InvalidCastException)
                    {
                        return;
                    }
                }
            }
            // Enable add image button after a L1 is selected
            button_addImage.Enabled = true;
        }

        public void OnL1Changed(string l1)
        {
            if ((string)objL1ListBox.SelectedItem == l1)
                objL1ListBox_SelectedIndexChanged(null, null);
        }

        private void objItem_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) // context menu when right clicked
            {
                // Show context menu for delete option
                ShowContextMenu((ImageViewer)sender, e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                lock (hcsm.MultiBoard)
                {
                    if (!hcsm.MultiBoard.AssertLayerSelected())
                    {
                        return;
                    }
                    hcsm.EnterEditMode(ItemTypes.Objects);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((ObjectInfo)((ImageViewer)sender).Tag);
                    hcsm.MultiBoard.Focus();
                    ((ImageViewer)sender).IsActive = true;
                }
            }
        }

        /// <summary>
        /// Adds an image to the obj
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addImage_Click(object sender, EventArgs e)
        {
            if (objSetListBox.SelectedItem == null || objL0ListBox.SelectedItem == null || objL1ListBox.SelectedItem == null)
            {
                MessageBox.Show(this.ResourceManager.GetString("SelectAnImageBefore"), this.ResourceManager.GetString("SelectAnImageBeforeTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        Bitmap newImage = new(openFileDialog.FileName);
                        string imageName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);

                        string objSetName = (string)objSetListBox.SelectedItem;
                        string l0Name = (string)objL0ListBox.SelectedItem;
                        string l1Name = (string)objL1ListBox.SelectedItem;

                        // Get the L1 property
                        WzImageProperty l1Prop = Program.InfoManager.ObjectSets[objSetName][l0Name][l1Name];

                        // Generate a unique name for the new object
                        string newObjL2Name = GenerateUniqueObjectName(objSetName, l0Name, l1Name);

                        ObjectInfo newObjectInfo;
                        WzImageProperty newL2Prop_;

                        // Create a new WzSubProperty for the L2 object
                        WzSubProperty newL2Prop = new WzSubProperty(newObjL2Name);

                        // Add necessary properties to the new L2 object
                        newL2Prop["z"] = new WzIntProperty("z", 0); // Default z-index
                        WzCanvasProperty canvasProp = new WzCanvasProperty("0");
                        canvasProp.PngProperty = new WzPngProperty();
                        canvasProp.PngProperty.PNG = newImage;
                        newL2Prop["0"] = canvasProp;

                        newL2Prop_ = newL2Prop;

                        // Add the new L2 property to the L1 property
                        l1Prop.WzProperties.Add(newL2Prop);

                        // there's no fixed place or consistency in the WZ
                        // sometimes its at the top-center
                        // sometimes bottom-left or bottom-right
                        // it depends on nexon-devs, better off making this automatic for the user.
                        Point point = new Point(newImage.Width / 2, newImage.Height); // set at bottom-center

                        // Create a new ObjectInfo
                        newObjectInfo = new ObjectInfo(newImage, point, objSetName, l0Name, l1Name, newObjL2Name, newL2Prop);

                        // Add the new image to the objImagesContainer
                        ImageViewer newItem = objImagesContainer.Add(newImage, newObjL2Name, true);
                        newItem.Tag = newObjectInfo;
                        newItem.MouseDown += new MouseEventHandler(objItem_Click);
                        newItem.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                        newItem.MaxHeight = UserSettings.ImageViewerHeight;
                        newItem.MaxWidth = UserSettings.ImageViewerWidth;

                        // flag WZ files changed to save it
                        WzObject topMostWzDir = l1Prop.GetTopMostWzDirectory();
                        WzObject topMostWzImg = l1Prop.GetTopMostWzImage();
                        Program.WzManager.SetWzFileUpdated(topMostWzDir.Name, topMostWzImg as WzImage);

                        MessageBox.Show(
                            string.Format(this.ResourceManager.GetString("ImageAddSuccessful"), openFileDialog.FileName, newObjL2Name), 
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
        private string GenerateUniqueObjectName(string objSetName, string l0Name, string l1Name)
        {
            int counter = 1;
            WzImageProperty l1Prop = Program.InfoManager.ObjectSets[objSetName][l0Name][l1Name];
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
        private void ShowContextMenu(ImageViewer item, Point location)
        {
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

            ObjectInfo objInfo = (ObjectInfo)selectedItem.Tag;

            WzImageProperty l2Prop = Program.InfoManager.ObjectSets[objInfo.oS]?[objInfo.l0]?[objInfo.l1]?[objInfo.l2];

            if (l2Prop != null)
            {
                WzSubProperty l2SubProp = (WzSubProperty)l2Prop;
                WzCanvasProperty l2SubImg = (WzCanvasProperty)l2SubProp["0"];
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
                    WzObject topMostWzDir = l2Prop.GetTopMostWzDirectory();
                    WzObject topMostWzImg = l2Prop.GetTopMostWzImage();
                    Program.WzManager.SetWzFileUpdated(topMostWzDir.Name, topMostWzImg as WzImage);
                }
            }
        }

        /// <summary>
        /// Event handler for save menu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveItem_Click(object sender, EventArgs e)
        {
            ImageViewer selectedItem = contextMenu.SourceControl as ImageViewer;
            if (selectedItem != null)
            {
                ObjectInfo objInfo = (ObjectInfo)selectedItem.Tag;

                if (objInfo.Image != null)
                {
                    System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog()
                    {
                        FileName = string.Format("{0}.{1}.{2}", objInfo.oS, objInfo.l0, objInfo.l1),
                        Title = "Select where to save the image...",
                        Filter = "Portable Network Graphics (*.png)|*.png|CompuServe Graphics Interchange Format (*.gif)|*.gif|Bitmap (*.bmp)|*.bmp|Joint Photographic Experts Group Format (*.jpg)|*.jpg|Tagged Image File Format (*.tif)|*.tif"
                    };
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;
                    switch (dialog.FilterIndex)
                    {
                        case 1: //png
                            objInfo.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                        case 2: //gif
                            objInfo.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Gif);
                            break;
                        case 3: //bmp
                            objInfo.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Bmp);
                            break;
                        case 4: //jpg
                            objInfo.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
                            break;
                        case 5: //tiff
                            objInfo.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Tiff);
                            break;
                    }
                }
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
                    // delete off cached obj
                    ObjectInfo objInfo = (ObjectInfo)selectedItem.Tag;

                    WzImageProperty l1Prop = Program.InfoManager.ObjectSets[objInfo.oS]?[objInfo.l0]?[objInfo.l1];

                    if (l1Prop != null)
                    {
                        WzImageProperty removeL2Prop = l1Prop[objInfo.l2];

                        if (l1Prop.WzProperties.Contains(removeL2Prop))
                        {
                            l1Prop.WzProperties.Remove(removeL2Prop);

                            // Perform delete operation
                            objImagesContainer.Remove(selectedItem);
                            selectedItem.Dispose();

                            // flag WZ files changed to save it
                            WzObject topMostWzDir = l1Prop.GetTopMostWzDirectory();
                            WzObject topMostWzImg = l1Prop.GetTopMostWzImage();
                            Program.WzManager.SetWzFileUpdated(topMostWzDir.Name, topMostWzImg as WzImage);
                        }
                    }
                }
            }
        }
        #endregion
    }
}
