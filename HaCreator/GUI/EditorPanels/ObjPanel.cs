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
using System.Linq;
using System.Windows.Forms;

namespace HaCreator.GUI.EditorPanels
{
    public partial class ObjPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        /// <summary>
        /// Constructor
        /// </summary>
        public ObjPanel()
        {
            InitializeComponent();
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

        /// <summary>
        /// Adds an image to the obj
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addImage_Click(object sender, EventArgs e)
        {
            if (objSetListBox.SelectedItem == null || objL0ListBox.SelectedItem == null || objL1ListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an object set, L0, and L1 category before adding an image.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                //openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
                openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
                openFileDialog.Title = "Select an image to add";

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

                        bool bAnimated = false;
                        if (bAnimated)  // if its canvas property, its 1 frame, otherwise its animated
                        {
                            // Create a new WzSubProperty for the L2 object
                            WzCanvasProperty newL2Prop = new WzCanvasProperty("0", l1Prop);
                            newL2Prop.PngProperty = new WzPngProperty();
                            newL2Prop.PngProperty.PNG = newImage;
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
                        }
                        else
                        {
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
                        }

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

                        MessageBox.Show($"Image '{newObjL2Name}' added successfully.", "Image Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    }
}
