/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Drawing;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Drawing.Imaging;
using WeifenLuo.WinFormsUI.Docking;
using System.Threading;
using HaRepacker.GUI.Interaction;
using MapleLib.WzLib.WzStructure.Data;
using System.Linq;
using HaRepacker;
using HaRepackerLib;
using HaRepackerLib.Controls;
using HaRepacker.Properties;
using HaRepacker.GUI.Input;
using HaRepacker.Configuration;

namespace HaRepacker.GUI.Panels
{
    public partial class HaRepackerMainPanel : UserControl
    {
        private static List<WzObject> clipboard = new List<WzObject>();
        private UndoRedoManager undoRedoMan;

        private bool isSelectingWzMapFieldLimit = false;

        public HaRepackerMainPanel()
        {
            InitializeComponent();
           
            MainSplitContainer.Parent = MainDockPanel;
            undoRedoMan = new UndoRedoManager(this);

            this.Load += HaRepackerMainPanel_Load;
        }

        /// <summary>
        /// On loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaRepackerMainPanel_Load(object sender, EventArgs e)
        {
            this.fieldLimitPanel1.SetTextboxOnFieldLimitChange(textPropBox);

             SetThemeColor();
        }

        #region Theme Colors
        public void SetThemeColor()
        {
            if (Program.ConfigurationManager.UserSettings.ThemeColor == 0) // black
            {
                this.BackColor = Color.DimGray;
                DataTree.BackColor = Color.Black;
                DataTree.ForeColor = Color.WhiteSmoke;
            }
            else
            {
                this.BackColor = Control.DefaultBackColor;
                DataTree.BackColor = Color.White;
                DataTree.ForeColor = DefaultForeColor;
            }
        }
        #endregion


        #region Handlers
        private void RedockControls()
        {
            if (Width * Height == 0)
                return;

            // Disabled for now... 
            cartesianPlaneX.Visible = false;
            cartesianPlaneY.Visible = false;

            //Point autoScrollPos = pictureBoxPanel.AutoScrollPosition;
            pictureBoxPanel.AutoScrollPosition = new Point();
            MainSplitContainer.Location = new Point(0, 0);
            MainSplitContainer.Size = new Size(Width, statusStrip.Location.Y - (findStrip.Visible ? findStrip.Height : 0));
            MainDockPanel.Location = new Point(0, 0);
            MainDockPanel.Size = MainSplitContainer.Size;
            DataTree.Location = new Point(0, 0);
            DataTree.Size = new Size(MainSplitContainer.Panel1.Width, MainSplitContainer.Panel1.Height);
            nameBox.Location = new Point(0, 0);
            nameBox.Size = new Size(MainSplitContainer.Panel2.Width, nameBox.Size.Height);

            pictureBoxPanel.Location = new Point(0, nameBox.Size.Height + nameBox.Margin.Bottom);
            pictureBoxPanel.Size = new Size(MainSplitContainer.Panel2.Width, MainSplitContainer.Panel2.Height - pictureBoxPanel.Location.Y - saveImageButton.Height - saveImageButton.Margin.Top);
            canvasPropBox.Location = new Point(0, 0);

            canvasPropBox.Size = canvasPropBox.Image == null ? new Size(0, 0) : canvasPropBox.Image.Size;

            textPropBox.Location = pictureBoxPanel.Location;
            textPropBox.Size = pictureBoxPanel.Size;
            mp3Player.Location = new Point((MainSplitContainer.Panel2.Width) / 2 - (mp3Player.Width / 2), MainSplitContainer.Height / 2 - mp3Player.Height / 2);
            vectorPanel.Location = new Point(MainSplitContainer.Panel2.Width / 2 - vectorPanel.Width / 2, MainSplitContainer.Height / 2 - vectorPanel.Height / 2);

            applyChangesButton.Location = new Point(MainSplitContainer.Panel2.Width / 2 - applyChangesButton.Width / 2, MainSplitContainer.Panel2.Height - applyChangesButton.Height);
            changeImageButton.Location = new Point(MainSplitContainer.Panel2.Width / 2 - (changeImageButton.Width + changeImageButton.Margin.Right + saveImageButton.Width) / 2, MainSplitContainer.Panel2.Height - changeImageButton.Height);
            saveImageButton.Location = new Point(changeImageButton.Location.X + changeImageButton.Width + changeImageButton.Margin.Right + 100, changeImageButton.Location.Y);
            changeSoundButton.Location = changeImageButton.Location;
            saveSoundButton.Location = saveImageButton.Location;

            // field limit type
            if (isSelectingWzMapFieldLimit)
            {
                fieldLimitPanel1.Visible = true;
                fieldLimitPanel1.Size = new Size(
                    MainSplitContainer.Panel2.Width,
                    MainSplitContainer.Panel2.Height - pictureBoxPanel.Location.Y - saveImageButton.Height - saveImageButton.Margin.Top - 20);

                textPropBox.Height = 30;
                textPropBox.Enabled = false;
            }
            else
            {
                fieldLimitPanel1.Visible = false;
                textPropBox.Height = MainSplitContainer.Panel2.Height;
                textPropBox.Enabled = true;
            }

            // 
            button_animateSelectedCanvas.Location = new Point(pictureBoxPanel.Width - button_animateSelectedCanvas.Size.Width - 15, 30);
            nextLoopTime_label.Location = new Point(nameBox.Width, 6);
            nextLoopTime_comboBox.SelectedIndex = 0;
            nextLoopTime_comboBox.Location = new Point(nextLoopTime_label.Location.X + nextLoopTime_label.Width + 2, 3);
            planePosition_comboBox.SelectedIndex = Program.ConfigurationManager.UserSettings.PlanePosition;
            planePosition_comboBox.Location = new Point(nextLoopTime_comboBox.Location.X + nextLoopTime_comboBox.Width + 5, 3);
            cartesianPlane_checkBox.Location = new Point(planePosition_comboBox.Location.X + planePosition_comboBox.Width + 3, 6);
            cartesianPlane_checkBox.Checked = Program.ConfigurationManager.UserSettings.Plane;
        }

        private void MainSplitContainer_SplitterMoved(object sender, SplitterEventArgs e)
        {
            RedockControls();
        }

        private void HaRepackerMainPanel_SizeChanged(object sender, EventArgs e)
        {
            RedockControls();
        }

        private void DataTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (DataTree.SelectedNode == null)
            {
                return;
            }

            ShowObjectValue((WzObject)DataTree.SelectedNode.Tag);
            selectionLabel.Text = string.Format(Properties.Resources.SelectionType, ((WzNode)DataTree.SelectedNode).GetTypeName());
        }

        /// <summary>
        /// On painting PictureBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanvasPropBox_Paint(object sender, PaintEventArgs e)
        {
        }

        /// <summary>
        /// Mouse move inside the picture panel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panel_pictureOrigin_OnMouseMove(object sender, MouseEventArgs e)
        {

        }

        /// <summary>
        /// Shows the selected data treeview object to UI
        /// </summary>
        /// <param name="obj"></param>
        private void ShowObjectValue(WzObject obj)
        {
            mp3Player.SoundProperty = null;
            nameBox.Text = obj is WzFile ? ((WzFile)obj).Header.Copyright : obj.Name;
            nameBox.ButtonEnabled = false;

            toolStripStatusLabel_additionalInfo.Text = "-"; // Reset additional info to default
            if (isSelectingWzMapFieldLimit) // previously already selected. update again
            {
                isSelectingWzMapFieldLimit = false;
                RedockControls();
            }

            // For animate pane
            vectorOriginSelected = new PointF(0,0);
            ShowOptionsCanvasAnimate(obj is WzCanvasProperty || obj is WzUOLProperty);

            // Other panels
            if (obj is WzFile || obj is WzDirectory || obj is WzImage || obj is WzNullProperty || obj is WzSubProperty || obj is WzConvexProperty)
            {
                nameBox.Visible = true;
                canvasPropBox.Visible = false;
                pictureBoxPanel.Visible = false;
                textPropBox.Visible = false;
                mp3Player.Visible = false;
                vectorPanel.Visible = false;
                applyChangesButton.Visible = false;
                changeImageButton.Visible = false;
                saveImageButton.Visible = false;
                changeSoundButton.Visible = false;
                saveSoundButton.Visible = false;
            }
            else if (obj is WzCanvasProperty)
            {
                nameBox.Visible = true;
                canvasPropBox.Visible = true;
                pictureBoxPanel.Visible = true;
                textPropBox.Visible = false;
                mp3Player.Visible = false;

                // Paint image
                WzCanvasProperty canvas = (WzCanvasProperty)obj;
                if (canvas.HaveInlinkProperty() || canvas.HaveOutlinkProperty())
                {
                    Image img = canvas.GetLinkedWzCanvasBitmap();
                    if (img != null)
                        canvasPropBox.Image = img;
                }
                else
                    canvasPropBox.Image = obj.GetBitmap();

                vectorPanel.Visible = false;
                applyChangesButton.Visible = false;
                changeImageButton.Visible = true;
                saveImageButton.Visible = true;
                changeSoundButton.Visible = false;
                saveSoundButton.Visible = false;
            }
            else if (obj is WzUOLProperty)
            {
                nameBox.Visible = true;
                textPropBox.Visible = true;
                mp3Player.Visible = false;
                pictureBoxPanel.Visible = true; // textPropBox is inside Picturebox
                textPropBox.Text = obj.ToString();
                vectorPanel.Visible = false;
                applyChangesButton.Visible = true;
                changeImageButton.Visible = false;
                changeSoundButton.Visible = false;
                saveSoundButton.Visible = false;

                WzObject linkValue = ((WzUOLProperty)obj).LinkValue;
                if (linkValue is WzCanvasProperty)
                {
                    canvasPropBox.Visible = true;
                    canvasPropBox.Image = linkValue.GetBitmap();
                    pictureBoxPanel.Visible = true;
                    saveImageButton.Visible = true;

                    textPropBox.Size = new Size(textPropBox.Size.Width, 50);
                }
                else
                {

                    canvasPropBox.Visible = false;
                    pictureBoxPanel.Visible = false;
                    saveImageButton.Visible = false;

                    textPropBox.Size = pictureBoxPanel.Size;
                }
            }
            else if (obj is WzSoundProperty)
            {
                nameBox.Visible = true;
                canvasPropBox.Visible = false;
                pictureBoxPanel.Visible = false;
                textPropBox.Visible = false;
                mp3Player.Visible = true;
                mp3Player.SoundProperty = (WzSoundProperty)obj;
                vectorPanel.Visible = false;
                applyChangesButton.Visible = false;
                changeImageButton.Visible = false;
                saveImageButton.Visible = false;
                changeSoundButton.Visible = true;
                saveSoundButton.Visible = true;
            }
            else if (obj is WzStringProperty || obj is WzIntProperty || obj is WzDoubleProperty || obj is WzFloatProperty || obj is WzShortProperty/* || obj is WzUOLProperty*/)
            {
                nameBox.Visible = true;
                canvasPropBox.Visible = false;
                pictureBoxPanel.Visible = true; // textPropBox is inside Picturebox
                textPropBox.Visible = true;
                mp3Player.Visible = false;
                textPropBox.Text = obj.ToString();
                vectorPanel.Visible = false;
                applyChangesButton.Visible = true;
                changeImageButton.Visible = false;
                saveImageButton.Visible = false;
                changeSoundButton.Visible = false;
                saveSoundButton.Visible = false;

                if (obj is WzStringProperty)
                {
                    WzStringProperty stringObj = (WzStringProperty)obj;

                    // Portal type name display
                    if (stringObj.Name == "pn") // "pn" = portal name
                    {
                        if (MapleLib.WzLib.WzStructure.Data.Tables.PortalTypeNames.ContainsKey(obj.GetString()))
                        {
                            toolStripStatusLabel_additionalInfo.Text =
                                string.Format(Resources.MainAdditionalInfo_PortalType, MapleLib.WzLib.WzStructure.Data.Tables.PortalTypeNames[obj.GetString()]);
                        }
                        else
                        {
                            toolStripStatusLabel_additionalInfo.Text = string.Format(Properties.Resources.MainAdditionalInfo_PortalType, obj.GetString());
                        }
                    }
                }
                else if (obj is WzIntProperty)
                {
                    WzIntProperty intProperty = (WzIntProperty)obj;

                    if (intProperty.Name == "fieldLimit")
                    {
                        isSelectingWzMapFieldLimit = true;

                        fieldLimitPanel1.UpdateFieldLimitCheckboxes(intProperty);

                        // Redock controls
                        RedockControls();
                    }
                }
            }
            else if (obj is WzVectorProperty)
            {
                nameBox.Visible = true;
                canvasPropBox.Visible = false;
                pictureBoxPanel.Visible = false;
                textPropBox.Visible = false;
                mp3Player.Visible = false;
                vectorPanel.Visible = true;
                vectorPanel.X = ((WzVectorProperty)obj).X.Value;
                vectorPanel.Y = ((WzVectorProperty)obj).Y.Value;
                applyChangesButton.Visible = true;
                changeImageButton.Visible = false;
                saveImageButton.Visible = false;
                changeSoundButton.Visible = false;
                saveSoundButton.Visible = false;
            }
            else
            {
            }
        }


        private void DataTree_DoubleClick(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode != null && DataTree.SelectedNode.Tag is WzImage && DataTree.SelectedNode.Nodes.Count == 0)
            {
                ParseOnDataTreeSelectedItem(((WzNode)DataTree.SelectedNode));
            }
        }

        /// <summary>
        /// Parse the data tree selected item on double clicking, or copy pasting into it.
        /// </summary>
        /// <param name="selectedNode"></param>
        private static void ParseOnDataTreeSelectedItem(WzNode selectedNode)
        {
            if (!((WzImage)selectedNode.Tag).Parsed)
                ((WzImage)selectedNode.Tag).ParseImage();
            selectedNode.Reparse();
            selectedNode.Expand();
        }
        #endregion

        #region Exported Fields
        public UndoRedoManager UndoRedoMan { get { return undoRedoMan; } }
        #endregion

        #region Save export
        /// <summary>
        /// Save button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveImageButton_Click(object sender, EventArgs e)
        {
            if (!(DataTree.SelectedNode.Tag is WzCanvasProperty) && !(DataTree.SelectedNode.Tag is WzUOLProperty))
            {
                return;
            }

            Bitmap wzCanvasPropertyObjLocation = null;

            if (DataTree.SelectedNode.Tag is WzCanvasProperty)
                wzCanvasPropertyObjLocation = ((WzCanvasProperty)DataTree.SelectedNode.Tag).GetLinkedWzCanvasBitmap();
            else
            {
                WzObject linkValue = ((WzUOLProperty)DataTree.SelectedNode.Tag).LinkValue;
                if (linkValue is WzCanvasProperty)
                {
                    wzCanvasPropertyObjLocation = ((WzCanvasProperty)linkValue).GetLinkedWzCanvasBitmap();
                }
                else
                    return;
            }
            if (wzCanvasPropertyObjLocation == null)
                return; // oops, we're fucked lulz

            SaveFileDialog dialog = new SaveFileDialog() { Title = "Select where to save the image...", Filter = "Portable Network Grpahics (*.png)|*.png|CompuServe Graphics Interchange Format (*.gif)|*.gif|Bitmap (*.bmp)|*.bmp|Joint Photographic Experts Group Format (*.jpg)|*.jpg|Tagged Image File Format (*.tif)|*.tif" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            switch (dialog.FilterIndex)
            {
                case 1: //png
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, ImageFormat.Png);
                    break;
                case 2: //gif
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, ImageFormat.Gif);
                    break;
                case 3: //bmp
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, ImageFormat.Bmp);
                    break;
                case 4: //jpg
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, ImageFormat.Jpeg);
                    break;
                case 5: //tiff
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, ImageFormat.Tiff);
                    break;
            }
        }

        private void saveSoundButton_Click(object sender, EventArgs e)
        {
            if (!(DataTree.SelectedNode.Tag is WzSoundProperty)) return;
            WzSoundProperty mp3 = (WzSoundProperty)DataTree.SelectedNode.Tag;
            SaveFileDialog dialog = new SaveFileDialog() { Title = "Select where to save the image...", Filter = "Moving Pictures Experts Group Format 1 Audio Layer 3 (*.mp3)|*.mp3" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            mp3.SaveToFile(dialog.FileName);
        }
        #endregion

        #region Image directory add
        /// <summary>
        /// WzDirectory
        /// </summary>
        /// <param name="target"></param>
        public void AddWzDirectoryToSelectedNode(TreeNode target)
        {
            if (!(target.Tag is WzDirectory) && !(target.Tag is WzFile))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            string name;
            if (!NameInputBox.Show(Properties.Resources.MainAddDir, 0, out name))
                return;

            bool added = false;

            WzObject obj = (WzObject)target.Tag;
            while (obj is WzFile || ((obj = obj.Parent) is WzFile))
            {
                WzFile topMostWzFileParent = (WzFile)obj;

                ((WzNode)target).AddObject(new WzDirectory(name, topMostWzFileParent), UndoRedoMan);
                added = true;
                break;
            }
            if (!added)
            {
                MessageBox.Show(Properties.Resources.MainTreeAddDirError);
            }
        }

        /// <summary>
        /// WzDirectory
        /// </summary>
        /// <param name="target"></param>
        public void AddWzImageToSelectedNode(TreeNode target)
        {
            string name;
            if (!(target.Tag is WzDirectory) && !(target.Tag is WzFile))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddImg, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzImage(name) { Changed = true }, UndoRedoMan);
        }

        /// <summary>
        /// WzByteProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzByteFloatToSelectedNode(TreeNode target)
        {
            string name;
            double? d;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!FloatingPointInputBox.Show(Properties.Resources.MainAddFloat, out name, out d))
                return;
            ((WzNode)target).AddObject(new WzFloatProperty(name, (float)d), UndoRedoMan);
        }

        /// <summary>
        /// WzCanvasProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzCanvasToSelectedNode(TreeNode target)
        {
            string name;
            List<Bitmap> bitmaps = new List<Bitmap>();
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!BitmapInputBox.Show(Properties.Resources.MainAddCanvas, out name, out bitmaps))
                return;

            WzNode wzNode = ((WzNode)target);

            int i = 0;
            foreach (Bitmap bmp in bitmaps)
            {
                WzCanvasProperty canvas = new WzCanvasProperty(bitmaps.Count == 1 ? name : (name + i));
                WzPngProperty pngProperty = new WzPngProperty();
                pngProperty.SetPNG(bmp);
                canvas.PngProperty = pngProperty;

                WzNode newInsertedNode = wzNode.AddObject(canvas, UndoRedoMan);
                // Add an additional WzVectorProperty with X Y of 0,0
                newInsertedNode.AddObject(new WzVectorProperty(name, new WzIntProperty("X", 0), new WzIntProperty("Y", 0)), UndoRedoMan);

                i++;
            }
        }

        /// <summary>
        /// WzCompressedInt
        /// </summary>
        /// <param name="target"></param>
        public void AddWzCompressedIntToSelectedNode(TreeNode target)
        {
            string name;
            int? value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!IntInputBox.Show(Properties.Resources.MainAddInt, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzIntProperty(name, (int)value), UndoRedoMan);
        }

        /// <summary>
        /// WzLongProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzLongToSelectedNode(TreeNode target)
        {
            string name;
            long? value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!LongInputBox.Show(Properties.Resources.MainAddInt, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzLongProperty(name, (long)value), UndoRedoMan);
        }

        /// <summary>
        /// WzConvexProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzConvexPropertyToSelectedNode(TreeNode target)
        {
            string name;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddConvex, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzConvexProperty(name), UndoRedoMan);
        }

        /// <summary>
        /// WzNullProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzDoublePropertyToSelectedNode(TreeNode target)
        {
            string name;
            double? d;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!FloatingPointInputBox.Show(Properties.Resources.MainAddDouble, out name, out d))
                return;
            ((WzNode)target).AddObject(new WzDoubleProperty(name, (double)d), UndoRedoMan);
        }

        /// <summary>
        /// WzNullProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzNullPropertyToSelectedNode(TreeNode target)
        {
            string name;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddNull, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzNullProperty(name), UndoRedoMan);
        }

        /// <summary>
        /// WzSoundProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzSoundPropertyToSelectedNode(TreeNode target)
        {
            string name;
            string path;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!SoundInputBox.Show(Properties.Resources.MainAddSound, out name, out path))
                return;
            ((WzNode)target).AddObject(new WzSoundProperty(name, path), UndoRedoMan);
        }

        /// <summary>
        /// WzStringProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzStringPropertyToSelectedIndex(TreeNode target)
        {
            string name;
            string value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameValueInputBox.Show(Properties.Resources.MainAddString, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzStringProperty(name, value), UndoRedoMan);
        }

        /// <summary>
        /// WzSubProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzSubPropertyToSelectedIndex(TreeNode target)
        {
            string name;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddSub, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzSubProperty(name), UndoRedoMan);
        }

        /// <summary>
        /// WzUnsignedShortProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzUnsignedShortPropertyToSelectedIndex(TreeNode target)
        {
            string name;
            int? value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!IntInputBox.Show(Properties.Resources.MainAddShort, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzShortProperty(name, (short)value), UndoRedoMan);
        }

        /// <summary>
        /// WzUOLProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzUOLPropertyToSelectedIndex(TreeNode target)
        {
            string name;
            string value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameValueInputBox.Show(Properties.Resources.MainAddLink, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzUOLProperty(name, value), UndoRedoMan);
        }

        /// <summary>
        /// WzVectorProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzVectorPropertyToSelectedIndex(TreeNode target)
        {
            string name;
            Point? pt;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!VectorInputBox.Show(Properties.Resources.MainAddVec, out name, out pt))
                return;
            ((WzNode)target).AddObject(new WzVectorProperty(name, new WzIntProperty("X", ((Point)pt).X), new WzIntProperty("Y", ((Point)pt).Y)), UndoRedoMan);
        }

        /// <summary>
        /// Remove selected nodes
        /// </summary>
        public void PromptRemoveSelectedTreeNodes()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmRemove))
            {
                return;
            }

            List<UndoRedoAction> actions = new List<UndoRedoAction>();

            TreeNode[] nodeArr = new TreeNode[DataTree.SelectedNodes.Count];
            DataTree.SelectedNodes.CopyTo(nodeArr, 0);

            foreach (WzNode node in nodeArr)
                if (!(node.Tag is WzFile) && node.Parent != null)
                {
                    actions.Add(UndoRedoManager.ObjectRemoved((WzNode)node.Parent, node));
                    node.Delete();
                }
            UndoRedoMan.AddUndoBatch(actions);
        }

        /// <summary>
        /// Rename an individual node
        /// </summary>
        public void PromptRenameSelectedTreeNode()
        {
            if (DataTree.SelectedNodes.Count != 1)
            {
                return;
            }

            string newName = "";
            TreeNode currentSelectedNode = DataTree.SelectedNodes[0] as TreeNode;
            WzNode wzNode = (WzNode)currentSelectedNode;

            if (RenameInputBox.Show(Properties.Resources.MainConfirmRename, wzNode.Text, out newName))
            {
                wzNode.ChangeName(newName);
            }
        }
        #endregion

        #region Animate
        /// <summary>
        /// On button click for animating canvas
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_animateSelectedCanvas_Click(object sender, EventArgs e)
        {
            StartAnimateSelectedCanvas();
        }

        private int i_animateCanvasNode = 0;
        private bool bCanvasAnimationActive = false;
        private List<Tuple<string, int, PointF, Bitmap>> animate_PreLoadImages = new List<Tuple<string, int, PointF, Bitmap>>(); // list of pre-loaded images for animation [Image name, delay, origin, image]
        /// <summary>
        /// Animate the list of selected canvases
        /// </summary>
        public void StartAnimateSelectedCanvas()
        {
            if (nextLoopTime_comboBox.SelectedIndex == 1)
                timerImgSequence.Interval = Program.TimeStartAnimateDefault;

            if (bCanvasAnimationActive) // currently animating
            {
                StopCanvasAnimation();
            }
            else if (DataTree.SelectedNodes.Count >= 1)
            {
                List<Tuple<string, int, PointF, Bitmap>> load_animate_PreLoadImages = new List<Tuple<string, int, PointF, Bitmap>>();

                // Check all selected nodes, make sure they're all images.
                // and add to a list
                bool loadSuccessfully = true;
                string loadErrorMsg = null;

                foreach (WzNode selNode in DataTree.SelectedNodes)
                {
                    WzObject obj =  (WzObject) selNode.Tag;

                    WzCanvasProperty canvasProperty;

                    bool isUOLProperty = obj is WzUOLProperty;
                    if (obj is WzCanvasProperty || isUOLProperty)
                    {
                        // Get image property
                        Bitmap image;
                        if (!isUOLProperty)
                        {
                            canvasProperty = ((WzCanvasProperty)obj);
                            image = canvasProperty.GetLinkedWzCanvasBitmap();
                        }
                        else
                        {
                            WzObject linkVal = ((WzUOLProperty)obj).LinkValue;
                            if (linkVal is WzCanvasProperty)
                            {
                                canvasProperty = ((WzCanvasProperty)linkVal);
                                image = canvasProperty.GetLinkedWzCanvasBitmap();
                            }
                            else
                            { // one of the WzUOLProperty link data isnt a canvas
                                loadSuccessfully = false;
                                loadErrorMsg = "Error loading WzUOLProperty ID: " + obj.Name;
                                break;
                            }
                        }

                        // Get delay property
                        int? delay = canvasProperty[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt();
                        if (delay == null)
                            delay = 0;

                        PointF origin = canvasProperty.GetCanvasVectorPosition();

                        // Add to the list of images to render
                        load_animate_PreLoadImages.Add(new Tuple<string, int, PointF, Bitmap>(obj.Name, (int)delay, origin, image));
                    }
                    else
                    {
                        loadSuccessfully = false;
                        loadErrorMsg = "One of the selected nodes is not a WzCanvasProperty type";
                        break;
                    }
                }

                if (!loadSuccessfully)
                {
                    MessageBox.Show(loadErrorMsg, "Animate", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    if (animate_PreLoadImages.Count > 0) // clear existing images
                        animate_PreLoadImages.Clear();

                    // Sort by image name
                    IOrderedEnumerable<Tuple<string, int, PointF, Bitmap>> sorted = load_animate_PreLoadImages.OrderBy(x => x, new SemiNumericComparer());
                    animate_PreLoadImages.Clear(); // clear existing
                    animate_PreLoadImages.AddRange(sorted);

                    // Start animation
                    bCanvasAnimationActive = true; // flag

                    timerImgSequence.Start();
                    button_animateSelectedCanvas.Text = "Stop";
                }
            }
            else
            {
                MessageBox.Show("Select two or more nodes WzCanvasProperty", "Selection", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /// <summary>
        /// Comparer for string names. in ascending order
        /// </summary>
        private class SemiNumericComparer : IComparer<Tuple<string, int, PointF, Bitmap>>
        {
            public int Compare(Tuple<string, int, PointF, Bitmap> s1, Tuple<string, int, PointF, Bitmap> s2)
            {
                if (IsNumeric(s1) && IsNumeric(s2))
                {
                    if (Convert.ToInt32(s1.Item1) > Convert.ToInt32(s2.Item1)) return 1;
                    if (Convert.ToInt32(s1.Item1) < Convert.ToInt32(s2.Item1)) return -1;
                    if (Convert.ToInt32(s1.Item1) == Convert.ToInt32(s2.Item1)) return 0;
                }

                if (IsNumeric(s1) && !IsNumeric(s2))
                    return -1;

                if (!IsNumeric(s1) && IsNumeric(s2))
                    return 1;

                return string.Compare(s1.Item1, s2.Item1, true);
            }

            private static bool IsNumeric(Tuple<string, int, PointF, Bitmap> value)
            {
                int parseInt = 0;
                return Int32.TryParse(value.Item1, out parseInt);
            }
        }

        /// <summary>
        /// Stop animating canvases
        /// </summary>
        public void StopCanvasAnimation()
        {
            i_animateCanvasNode = 0;
            timerImgSequence.Stop();
            timerImgSequence.Interval = Program.TimeStartAnimateDefault;
            button_animateSelectedCanvas.Text = "Animate";

            bCanvasAnimationActive = false; // flag
        }

        private void timerImgSequence_Tick(object sender, EventArgs e)
        {
            if (i_animateCanvasNode >= animate_PreLoadImages.Count) // last animate node, reset to 0 next
            {
                if (nextLoopTime_comboBox.SelectedIndex != 0)
                {
                    canvasPropBox.Image = Properties.Resources.img_default;
                    toolStripStatusLabel_additionalInfo.Text = "Waiting " + Program.ConfigurationManager.UserSettings.DelayNextLoop + " ms.";
                    timerImgSequence.Interval = Program.ConfigurationManager.UserSettings.DelayNextLoop;
                    i_animateCanvasNode = 0;
                }
                else
                {
                    i_animateCanvasNode = 0;
                }
            }

            Tuple<string, int, PointF, Bitmap> currentNode = animate_PreLoadImages[i_animateCanvasNode];
            i_animateCanvasNode++; // increment 1

            // Set vector origin
            vectorOriginSelected = currentNode.Item3;
            RefreshCanvasLocation();

            // Set current image
            canvasPropBox.Image = currentNode.Item4;

            // Set tooltip text
            if (i_animateCanvasNode == animate_PreLoadImages.Count)
                toolStripStatusLabel_additionalInfo.Text = "# " + currentNode.Item1 + ", Delay: " + currentNode.Item2 + " ms. Repeating Animate.";
            else
                toolStripStatusLabel_additionalInfo.Text = "# " + currentNode.Item1 + ", Delay: " + currentNode.Item2+ " ms.";
            timerImgSequence.Interval = currentNode.Item2;
        }

        private void cartesianPlane_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (cartesianPlane_checkBox.Checked)
                Program.ConfigurationManager.UserSettings.Plane = true;
            else
                Program.ConfigurationManager.UserSettings.Plane = false;

            //cartesianPlaneX.Visible = UserSettings.Plane;
            //cartesianPlaneY.Visible = UserSettings.Plane;
        }

        private void nextLoopTime_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (nextLoopTime_comboBox == null)
                return;

            switch (nextLoopTime_comboBox.SelectedIndex)
            {
                case 1:
                    Program.ConfigurationManager.UserSettings.DelayNextLoop = 1000;
                    break;
                case 2:
                    Program.ConfigurationManager.UserSettings.DelayNextLoop = 2000;
                    break;
                case 3:
                    Program.ConfigurationManager.UserSettings.DelayNextLoop = 5000;
                    break;
                case 4:
                    Program.ConfigurationManager.UserSettings.DelayNextLoop = 10000;
                    break;
                default:
                    Program.ConfigurationManager.UserSettings.DelayNextLoop = Program.TimeStartAnimateDefault;
                    break;
            }
        }

        private void ShowOptionsCanvasAnimate(bool visible)
        {
            nextLoopTime_label.Visible = visible;
            nextLoopTime_comboBox.Visible = visible;
            cartesianPlane_checkBox.Visible = visible;

            if (DataTree.SelectedNodes.Count <= 1)
                button_animateSelectedCanvas.Visible = false; // set invisible regardless if none of the nodes are selected.
            else
                button_animateSelectedCanvas.Visible = visible;

            planePosition_comboBox.Visible = visible;
            if (visible)
            {
                UpdatePlanePosition();
                //cartesianPlaneX.Visible = UserSettings.Plane;
                //cartesianPlaneY.Visible = UserSettings.Plane;
            }
            else
            {
                //cartesianPlaneX.Visible = false;
               // cartesianPlaneY.Visible = false;
            }
        }

        private PointF vectorOriginSelected;
        private void RefreshCanvasLocation()
        {
            if (Program.ConfigurationManager.UserSettings.DevImgSequences && vectorOriginSelected != null)
            {
                UpdatePlanePosition();
                ShowOptionsCanvasAnimate(true);
            }
            else
            {
                ShowOptionsCanvasAnimate(false);
                canvasPropBox.Location = new Point(0, 0);
            }
        }

        private void UpdatePlanePosition()
        {
            if (vectorOriginSelected == null || (vectorOriginSelected.X == 0 && vectorOriginSelected.Y == 0))
                return;

            int X = ((fieldLimitPanel1.Width / 2) * 90) / 100,  // 90%
                Y = ((fieldLimitPanel1.Height / 2) * 90) / 100, // 90%
                planeX__Y = pictureBoxPanel.Height / 2 + 10,
                planeY__X = pictureBoxPanel.Width / 2 - 6,
                canvasX = fieldLimitPanel1.Width / 2 - (int) vectorOriginSelected.X,
                canvasY = fieldLimitPanel1.Height / 2 - (int)vectorOriginSelected.Y;

            switch (Program.ConfigurationManager.UserSettings.PlanePosition)
            {
                case 1:// Top
                    canvasPropBox.Location = new Point(canvasX, canvasY - Y);
                    cartesianPlaneX.Location = new Point(0, planeX__Y - Y);
                    cartesianPlaneY.Location = new Point(planeY__X, 0);
                    break;
                case 2:// Bottom
                    canvasPropBox.Location = new Point(canvasX, canvasY + Y);
                    cartesianPlaneX.Location = new Point(0, planeX__Y + Y);
                    cartesianPlaneY.Location = new Point(planeY__X, 0);
                    break;
                case 3:// Right
                    canvasPropBox.Location = new Point(canvasX - X, canvasY);
                    cartesianPlaneX.Location = new Point(0, planeX__Y);
                    cartesianPlaneY.Location = new Point(planeY__X - X, 0);
                    break;
                case 4:// Left
                    canvasPropBox.Location = new Point(canvasX + X, canvasY);
                    cartesianPlaneX.Location = new Point(0, planeX__Y);
                    cartesianPlaneY.Location = new Point(planeY__X + X, 0);
                    break;
                case 5:
                    canvasPropBox.Location = new Point(canvasX - X, canvasY - Y);
                    cartesianPlaneX.Location = new Point(0, planeX__Y - Y);
                    cartesianPlaneY.Location = new Point(planeY__X - X, 0);
                    break;
                case 6:
                    canvasPropBox.Location = new Point(canvasX - X, canvasY + Y);
                    cartesianPlaneX.Location = new Point(0, planeX__Y + Y);
                    cartesianPlaneY.Location = new Point(planeY__X - X, 0);
                    break;
                case 7:
                    canvasPropBox.Location = new Point(canvasX + X, canvasY - Y);
                    cartesianPlaneX.Location = new Point(0, planeX__Y - Y);
                    cartesianPlaneY.Location = new Point(planeY__X + X, 0);
                    break;
                case 8:
                    canvasPropBox.Location = new Point(canvasX + X, canvasY + Y);
                    cartesianPlaneX.Location = new Point(0, planeX__Y + Y);
                    cartesianPlaneY.Location = new Point(planeY__X + X, 0);
                    break;
                default:
                    canvasPropBox.Location = new Point(canvasX, canvasY);
                    cartesianPlaneX.Location = new Point(0, planeX__Y);
                    cartesianPlaneY.Location = new Point(planeY__X, 0);
                    break;
            }
        }
        #endregion

        private void nameBox_ButtonClicked(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode == null) return;
            if (DataTree.SelectedNode.Tag is WzFile)
            {
                ((WzFile)DataTree.SelectedNode.Tag).Header.Copyright = nameBox.Text;
                ((WzFile)DataTree.SelectedNode.Tag).Header.RecalculateFileStart();
            }
            else if (WzNode.CanNodeBeInserted((WzNode)DataTree.SelectedNode.Parent, nameBox.Text))
            {
                string text = nameBox.Text;
                ((WzNode)DataTree.SelectedNode).ChangeName(text);
                nameBox.Text = text;
                nameBox.ButtonEnabled = false;
            }
            else
                Warning.Error(Properties.Resources.MainNodeExists);

        }

        private void applyChangesButton_Click(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode == null) return;
            WzObject obj = (WzObject)DataTree.SelectedNode.Tag;
            if (obj is WzImageProperty)
                ((WzImageProperty)obj).ParentImage.Changed = true;
            if (obj is WzVectorProperty)
            {
                ((WzVectorProperty)obj).X.Value = vectorPanel.X;
                ((WzVectorProperty)obj).Y.Value = vectorPanel.Y;
            }
            else if (obj is WzStringProperty)
                ((WzStringProperty)obj).Value = textPropBox.Text;
            else if (obj is WzFloatProperty)
            {
                float val;
                if (!float.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzFloatProperty)obj).Value = val;
            }
            else if (obj is WzIntProperty)
            {
                int val;
                if (!int.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzIntProperty)obj).Value = val;
            }
            else if (obj is WzDoubleProperty)
            {
                double val;
                if (!double.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzDoubleProperty)obj).Value = val;
            }
            else if (obj is WzShortProperty)
            {
                short val;
                if (!short.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzShortProperty)obj).Value = val;
            }
            else if (obj is WzUOLProperty)
            {
                ((WzUOLProperty)obj).Value = textPropBox.Text;
            }
        }

        /// <summary>
        /// Changing the image of WzCanvasProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void changeImageButton_Click(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode.Tag is WzCanvasProperty)
            {
                OpenFileDialog dialog = new OpenFileDialog() { Title = "Select the image", Filter = "Supported Image Formats (*.png;*.bmp;*.jpg;*.gif;*.jpeg;*.tif;*.tiff)|*.png;*.bmp;*.jpg;*.gif;*.jpeg;*.tif;*.tiff" };
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Bitmap bmp;
                try
                {
                    bmp = (Bitmap)Image.FromFile(dialog.FileName);
                }
                catch
                {
                    Warning.Error(Properties.Resources.MainImageLoadError);
                    return;
                }
                //List<UndoRedoAction> actions = new List<UndoRedoAction>(); // Undo action

                WzCanvasProperty selectedWzCanvas = (WzCanvasProperty)DataTree.SelectedNode.Tag;
                if (selectedWzCanvas.HaveInlinkProperty()) // if its an inlink property, remove that before updating base image.
                {
                    selectedWzCanvas.RemoveProperty(selectedWzCanvas[WzCanvasProperty.InlinkPropertyName]);

                    WzNode parentCanvasNode = (WzNode)DataTree.SelectedNode;
                    WzNode childInlinkNode = WzNode.GetChildNode(parentCanvasNode, WzCanvasProperty.InlinkPropertyName);

                    // Add undo actions
                    //actions.Add(UndoRedoManager.ObjectRemoved((WzNode)parentCanvasNode, childInlinkNode));
                    childInlinkNode.Delete(); // Delete '_inlink' node
                }
                else if (selectedWzCanvas.HaveOutlinkProperty()) // if its an inlink property, remove that before updating base image.
                {
                    selectedWzCanvas.RemoveProperty(selectedWzCanvas[WzCanvasProperty.OutlinkPropertyName]);

                    WzNode parentCanvasNode = (WzNode)DataTree.SelectedNode;
                    WzNode childInlinkNode = WzNode.GetChildNode(parentCanvasNode, WzCanvasProperty.OutlinkPropertyName);

                    // Add undo actions
                    //actions.Add(UndoRedoManager.ObjectRemoved((WzNode)parentCanvasNode, childInlinkNode));
                }
                else
                {

                }
                selectedWzCanvas.PngProperty.SetPNG(bmp);

                // Updates
                selectedWzCanvas.ParentImage.Changed = true;
                canvasPropBox.Image = bmp;

                // Add undo actions
                //UndoRedoMan.AddUndoBatch(actions);
            }
        }

        private void changeSoundButton_Click(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode.Tag is WzSoundProperty)
            {
                OpenFileDialog dialog = new OpenFileDialog() { Title = "Select the sound", Filter = "Moving Pictures Experts Group Format 1 Audio Layer 3(*.mp3)|*.mp3" };
                if (dialog.ShowDialog() != DialogResult.OK) return;
                WzSoundProperty prop;
                try
                {
                    prop = new WzSoundProperty(((WzSoundProperty)DataTree.SelectedNode.Tag).Name, dialog.FileName);
                }
                catch
                {
                    Warning.Error(Properties.Resources.MainImageLoadError);
                    return;
                }
                IPropertyContainer parent = (IPropertyContainer)((WzSoundProperty)DataTree.SelectedNode.Tag).Parent;
                ((WzSoundProperty)DataTree.SelectedNode.Tag).ParentImage.Changed = true;
                ((WzSoundProperty)DataTree.SelectedNode.Tag).Remove();
                DataTree.SelectedNode.Tag = prop;
                parent.AddProperty(prop);
                mp3Player.SoundProperty = prop;
            }
        }

        #region Copy & Paste
        public WzObject CloneWzObject(WzObject obj)
        {
            if (obj is WzDirectory)
            {
                Warning.Error(Properties.Resources.MainCopyDirError);
                return null;
            }
            else if (obj is WzImage)
            {
                return ((WzImage)obj).DeepClone();
            }
            else if (obj is WzImageProperty)
            {
                return ((WzImageProperty)obj).DeepClone();
            }
            else
            {
                MapleLib.Helpers.ErrorLogger.Log(MapleLib.Helpers.ErrorLevel.MissingFeature, "The current WZ object type cannot be cloned " + obj.ToString() + " " + obj.FullPath);
                return null;
            }
        }

        /// <summary>
        /// Copies from the selected Wz object
        /// </summary>
        public void DoCopy()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmCopy))
                return;

            clipboard.Clear();
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                WzObject clone = CloneWzObject((WzObject)node.Tag);
                if (clone != null)
                    clipboard.Add(clone);
            }
        }

        private ReplaceResult replaceBoxResult = ReplaceResult.NoneSelectedYet;

        /// <summary>
        /// Paste to the selected WzObject
        /// </summary>
        public void DoPaste()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmPaste))
                return;

            // Reset replace option
            replaceBoxResult = ReplaceResult.NoneSelectedYet;

            WzNode parent = (WzNode)DataTree.SelectedNode;
            WzObject parentObj = (WzObject)parent.Tag;

            if (parent != null && parent.Tag is WzImage && parent.Nodes.Count == 0)
            {
                ParseOnDataTreeSelectedItem(parent);
            }

            if (parentObj is WzFile)
                parentObj = ((WzFile)parentObj).WzDirectory;

            bool bNoToAllComplete = false;
            foreach (WzObject obj in clipboard)
            {
                if (((obj is WzDirectory || obj is WzImage) && parentObj is WzDirectory) || (obj is WzImageProperty && parentObj is IPropertyContainer))
                {
                    WzObject clone = CloneWzObject(obj);
                    if (clone == null)
                        continue;
                    WzNode node = new WzNode(clone, true);

                    WzNode child = WzNode.GetChildNode(parent, node.Text);
                    if (child != null) // A Child already exist
                    {
                        if (replaceBoxResult == ReplaceResult.NoneSelectedYet)
                        {
                            ReplaceBox.Show(node.Text, out replaceBoxResult);
                        }

                        switch (replaceBoxResult)
                        {
                            case ReplaceResult.No: // Skip just this
                                replaceBoxResult = ReplaceResult.NoneSelectedYet; // reset after use
                                break;

                            case ReplaceResult.Yes: // Replace just this
                                child.Delete();
                                parent.AddNode(node);
                                replaceBoxResult = ReplaceResult.NoneSelectedYet; // reset after use
                                break;

                            case ReplaceResult.NoToAll:
                                bNoToAllComplete = true;
                                break;

                            case ReplaceResult.YesToAll:
                                child.Delete();
                                parent.AddNode(node);
                                break;
                        }

                        if (bNoToAllComplete)
                            break;
                    }
                    else // not not in this 
                    {
                        parent.AddNode(node);
                    }
                }
            }
        }
        #endregion

        private void DataTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (!DataTree.Focused) return;
            bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            bool alt = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;
            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            Keys filteredKeys = e.KeyData;
            if (ctrl) filteredKeys = filteredKeys ^ Keys.Control;
            if (alt) filteredKeys = filteredKeys ^ Keys.Alt;
            if (shift) filteredKeys = filteredKeys ^ Keys.Shift;

            switch (filteredKeys)
            {
                case Keys.F5:
                    StartAnimateSelectedCanvas();
                    break;
                case Keys.Escape:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    StopCanvasAnimation();
                    break;

                case Keys.Delete:
                    e.Handled = true;
                    e.SuppressKeyPress = true;

                    PromptRemoveSelectedTreeNodes();
                    break;
            }
            if (ctrl)
            {
                switch (filteredKeys)
                {
                    case Keys.R: // Render map        
                        //HaRepackerMainPanel.

                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case Keys.C:
                        DoCopy();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case Keys.V:
                        DoPaste();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case Keys.F:
                        if (findStrip.Visible == true)
                        {
                            findBox.Focus();
                        }
                        findStrip.Visible = true;
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case Keys.T:
                    case Keys.O:
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                }
            }
        }

        private int searchidx = 0;
        private bool finished = false;
        private bool listSearchResults = false;
        private List<string> searchResultsList = new List<string>();
        private bool searchValues = true;
        private WzNode coloredNode = null;
        private int currentidx = 0;
        private string searchText = "";
        private bool extractImages = false;

        private void btnClose_Click(object sender, EventArgs e)
        {
            findStrip.Visible = false;
            searchidx = 0;
            if (coloredNode != null)
            {
                coloredNode.BackColor = Color.White;
                coloredNode = null;
            }
        }


        private void SearchWzProperties(IPropertyContainer parent)
        {
            foreach (WzImageProperty prop in parent.WzProperties)
            {
                if ((0 <= prop.Name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase)) || (searchValues && prop is WzStringProperty && (0 <= ((WzStringProperty)prop).Value.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase))))
                {
                    if (listSearchResults)
                        searchResultsList.Add(prop.FullPath.Replace(";", @"\"));
                    else if (currentidx == searchidx)
                    {
                        if (prop.HRTag == null)
                            ((WzNode)prop.ParentImage.HRTag).Reparse();
                        WzNode node = (WzNode)prop.HRTag;
                        //if (node.Style == null) node.Style = new ElementStyle();
                        node.BackColor = Color.Yellow;
                        coloredNode = node;
                        node.EnsureVisible();
                        //DataTree.Focus();
                        finished = true;
                        searchidx++;
                        return;
                    }
                    else
                        currentidx++;
                }
                if (prop is IPropertyContainer && prop.WzProperties.Count != 0)
                {
                    SearchWzProperties((IPropertyContainer)prop);
                    if (finished)
                        return;
                }
            }
        }

        private void SearchTV(WzNode node)
        {
            foreach (WzNode subnode in node.Nodes)
            {
                if (0 <= subnode.Text.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (listSearchResults)
                        searchResultsList.Add(subnode.FullPath.Replace(";", @"\"));
                    else if (currentidx == searchidx)
                    {
                        //if (subnode.Style == null) subnode.Style = new ElementStyle();
                        subnode.BackColor = Color.Yellow;
                        coloredNode = subnode;
                        subnode.EnsureVisible();
                        //DataTree.Focus();
                        finished = true;
                        searchidx++;
                        return;
                    }
                    else
                        currentidx++;
                }
                if (subnode.Tag is WzImage)
                {
                    WzImage img = (WzImage)subnode.Tag;
                    if (img.Parsed)
                        SearchWzProperties(img);
                    else if (extractImages)
                    {
                        img.ParseImage();
                        SearchWzProperties(img);
                    }
                    if (finished) return;
                }
                else SearchTV(subnode);
            }
        }

        private void btnRestart_Click(object sender, EventArgs e)
        {
            searchidx = 0;
            if (coloredNode != null)
            {
                coloredNode.BackColor = Color.White;
                coloredNode = null;
            }
            findBox.Focus();
        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            new SearchOptionsForm().ShowDialog();
            findBox.Focus();
        }

        private void btnFindNext_Click(object sender, EventArgs e)
        {
            if (coloredNode != null)
            {
                coloredNode.BackColor = Color.White;
                coloredNode = null;
            }
            if (findBox.Text == "" || DataTree.Nodes.Count == 0) return;
            if (DataTree.SelectedNode == null) DataTree.SelectedNode = DataTree.Nodes[0];
            finished = false;
            listSearchResults = false;
            searchResultsList.Clear();
            searchValues = Program.ConfigurationManager.UserSettings.SearchStringValues;
            currentidx = 0;
            searchText = findBox.Text;
            extractImages = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                if (node.Tag is IPropertyContainer)
                    SearchWzProperties((IPropertyContainer)node.Tag);
                else if (node.Tag is WzImageProperty) continue;
                else SearchTV(node);
                if (finished) break;
            }
            if (!finished) { MessageBox.Show(Properties.Resources.MainTreeEnd); searchidx = 0; DataTree.SelectedNode.EnsureVisible(); }
            findBox.Focus();
        }

        private void btnFindAll_Click(object sender, EventArgs e)
        {
            if (coloredNode != null)
            {
                coloredNode.BackColor = Color.White;
                coloredNode = null;
            }
            if (findBox.Text == "" || DataTree.Nodes.Count == 0) return;
            if (DataTree.SelectedNode == null) DataTree.SelectedNode = DataTree.Nodes[0];
            finished = false;
            listSearchResults = true;
            searchResultsList.Clear();
            //searchResultsBox.Items.Clear();
            searchValues = Program.ConfigurationManager.UserSettings.SearchStringValues;
            currentidx = 0;
            searchText = findBox.Text;
            extractImages = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                if (node.Tag is WzImageProperty) continue;
                else if (node.Tag is IPropertyContainer)
                    SearchWzProperties((IPropertyContainer)node.Tag);
                else SearchTV(node);
            }
            DockableSearchResult dsr = new DockableSearchResult();
            dsr.SelectedIndexChanged += new EventHandler(searchResultsBox_SelectedIndexChanged);
            foreach (string result in searchResultsList)
                dsr.searchResultsBox.Items.Add(result);
            dsr.Show(MainDockPanel);
            dsr.DockState = DockState.DockBottom;
            //            searchResults.AutoHide = false;
            //            searchResults.Visible = true;
            //            searchResultsContainer.Visible = true;
            //            dockSite8.Visible = true;
            //            panelDockContainer1.Visible = true;
            findBox.Focus();
        }

        private void findBox_TextChanged(object sender, EventArgs e)
        {
            searchidx = 0;
        }

        private void findBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { btnFindNext_Click(null, null); e.Handled = true; }
        }

        private void findBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) e.Handled = true;
        }

        private void findStrip_VisibleChanged(object sender, EventArgs e)
        {
            RedockControls();
            if (findStrip.Visible) findBox.Focus();
        }

        private void searchResults_VisibleChanged(object sender, EventArgs e)
        {
            RedockControls();
        }

        private void searchResultsContainer_VisibleChanged(object sender, System.EventArgs e)
        {
            RedockControls();
        }

        private WzNode GetNodeByName(TreeNodeCollection collection, string name)
        {
            foreach (WzNode node in collection)
                if (node.Text == name)
                    return node;
            return null;
        }

        private void searchResultsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBox searchResultsBox = (ListBox)sender;
            try
            {
                if (searchResultsBox.SelectedItem != null)
                {
                    string[] splitPath = ((string)searchResultsBox.SelectedItem).Split(@"\".ToCharArray());
                    WzNode node = null;
                    TreeNodeCollection collection = DataTree.Nodes;
                    for (int i = 0; i < splitPath.Length; i++)
                    {
                        node = GetNodeByName(collection, splitPath[i]);
                        if (node.Tag is WzImage && !((WzImage)node.Tag).Parsed && i != splitPath.Length - 1)
                        {
                            ((WzImage)node.Tag).ParseImage();
                            node.Reparse();
                        }
                        collection = node.Nodes;
                    }
                    if (node != null)
                    {
                        DataTree.SelectedNode = node;
                        node.EnsureVisible();
                        DataTree.RefreshSelectedNodes();
                    }
                }
            }
            catch
            {
            }
        }
    }
}
