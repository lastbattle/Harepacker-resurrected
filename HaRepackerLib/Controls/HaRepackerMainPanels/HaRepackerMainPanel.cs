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

namespace HaRepackerLib.Controls.HaRepackerMainPanels
{
    public partial class HaRepackerMainPanel : UserControl
    {
        private static List<WzObject> clipboard = new List<WzObject>();
        private UndoRedoManager undoRedoMan;

        // misc
        private bool isSelectingWzMapFieldLimit = false;
        private bool initializingListViewForFieldLimit = false;

        public HaRepackerMainPanel()
        {
            InitializeComponent();

            PopulateDefaultListView();

            MainSplitContainer.Parent = MainDockPanel;
            undoRedoMan = new UndoRedoManager(this);
        }

        #region Handlers
        private void PopulateDefaultListView()
        {
            initializingListViewForFieldLimit = true;

            // Populate FieldLimitType
            if (listView_fieldLimitType.Items.Count == 0)
            {
                // dummy column
                listView_fieldLimitType.Columns.Add(new ColumnHeader()
                {
                    Text = "",
                    Name = "col1",
                    Width = 450,
                });

                int i_index = 0;
                foreach (WzFieldLimitType limitType in Enum.GetValues(typeof(WzFieldLimitType)))
                {
                    ListViewItem item1 = new ListViewItem(
                        string.Format("{0} - {1}", (i_index).ToString(), limitType.ToString().Replace("_", " ")));
                    item1.Tag = limitType; // starts from 0
                    listView_fieldLimitType.Items.Add(item1);

                    i_index++;
                }
                for (int i = i_index; i < i_index + 50; i++) // add 50 dummy values, we really dont have the field properties of future MS versions :( 
                {
                    ListViewItem item1 = new ListViewItem(string.Format("{0} - UNKNOWN", (i).ToString()));
                    item1.Tag = i;
                    listView_fieldLimitType.Items.Add(item1);
                }
            }

            initializingListViewForFieldLimit = false;
        }

        private void RedockControls()
        {
            if (Width * Height == 0)
                return;

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
            mp3Player.Location = new Point(MainSplitContainer.Panel2.Width / 2 - mp3Player.Width / 2, MainSplitContainer.Height / 2 - mp3Player.Height / 2);
            vectorPanel.Location = new Point(MainSplitContainer.Panel2.Width / 2 - vectorPanel.Width / 2, MainSplitContainer.Height / 2 - vectorPanel.Height / 2);

            applyChangesButton.Location = new Point(MainSplitContainer.Panel2.Width / 2 - applyChangesButton.Width / 2, MainSplitContainer.Panel2.Height - applyChangesButton.Height);
            changeImageButton.Location = new Point(MainSplitContainer.Panel2.Width / 2 - (changeImageButton.Width + changeImageButton.Margin.Right + saveImageButton.Width) / 2, MainSplitContainer.Panel2.Height - changeImageButton.Height);
            saveImageButton.Location = new Point(changeImageButton.Location.X + changeImageButton.Width + changeImageButton.Margin.Right + 100, changeImageButton.Location.Y);
            changeSoundButton.Location = changeImageButton.Location;
            saveSoundButton.Location = saveImageButton.Location;

            if (isSelectingWzMapFieldLimit)
            {
                listView_fieldLimitType.Visible = true;
                listView_fieldLimitType.Size = new Size(
                    MainSplitContainer.Panel2.Width,
                    MainSplitContainer.Panel2.Height - pictureBoxPanel.Location.Y - saveImageButton.Height - saveImageButton.Margin.Top - 20);

                textPropBox.Height = 30;
                textPropBox.Enabled = false;
            }
            else
            {
                listView_fieldLimitType.Visible = false;
                textPropBox.Height = MainSplitContainer.Panel2.Height;
                textPropBox.Enabled = true;
            }
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
                pictureBoxPanel.Visible = false;
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
                                string.Format(Properties.Resources.MainAdditionalInfo_PortalType, MapleLib.WzLib.WzStructure.Data.Tables.PortalTypeNames[obj.GetString()]);
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


                        initializingListViewForFieldLimit = true;

                        // Fill checkboxes
                        //int maxFieldLimitType = FieldLimitTypeExtension.GetMaxFieldLimitType();
                        foreach (ListViewItem item in listView_fieldLimitType.Items)
                        {
                            item.Checked = FieldLimitTypeExtension.Check((int)item.Tag, intProperty.Value);
                        }
                        initializingListViewForFieldLimit = false;
                        ListView_fieldLimitType_ItemChecked(listView_fieldLimitType, null);

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

        /// <summary>
        /// On WzFieldLimitType listview item checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_fieldLimitType_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (initializingListViewForFieldLimit)
                return;

            System.Diagnostics.Debug.WriteLine("Set index at  " + e.Index + " to " + listView_fieldLimitType.Items[e.Index].Checked);
        }

        /// <summary>
        /// On WzFieldLimitType listview item checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_fieldLimitType_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (initializingListViewForFieldLimit)
                return;

            ulong fieldLimit = 0;
            foreach (ListViewItem item in listView_fieldLimitType.Items)
            {
                if (item.Checked)
                {
                    int numShift = ((int)item.Tag);

                    System.Diagnostics.Debug.WriteLine("Selected " + numShift + ", " + (long)(1L << numShift));
                    fieldLimit |= (ulong)(1L << numShift);
                }
            }
            System.Diagnostics.Debug.WriteLine("Result " + fieldLimit);
            textPropBox.Text = fieldLimit.ToString();
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

        #region Image directory add
        /// <summary>
        /// WzDirectory
        /// </summary>
        /// <param name="target"></param>
        public void AddWzDirectoryToSelectedNode(TreeNode target)
        {
            if (!(target.Tag is WzDirectory) && !(target.Tag is WzFile))
            {
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            string name;
            if (!NameInputBox.Show(HaRepackerLib.Properties.Resources.MainAddDir, out name))
                return;

            ((WzNode)target).AddObject(new WzDirectory(name), UndoRedoMan);
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(HaRepackerLib.Properties.Resources.MainAddImg, out name))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!FloatingPointInputBox.Show(HaRepackerLib.Properties.Resources.MainAddFloat, out name, out d))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!BitmapInputBox.Show(HaRepackerLib.Properties.Resources.MainAddCanvas, out name, out bitmaps))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!IntInputBox.Show(HaRepackerLib.Properties.Resources.MainAddInt, out name, out value))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!LongInputBox.Show(HaRepackerLib.Properties.Resources.MainAddInt, out name, out value))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(HaRepackerLib.Properties.Resources.MainAddConvex, out name))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!FloatingPointInputBox.Show(HaRepackerLib.Properties.Resources.MainAddDouble, out name, out d))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(HaRepackerLib.Properties.Resources.MainAddNull, out name))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!SoundInputBox.Show(HaRepackerLib.Properties.Resources.MainAddSound, out name, out path))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameValueInputBox.Show(HaRepackerLib.Properties.Resources.MainAddString, out name, out value))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(HaRepackerLib.Properties.Resources.MainAddSub, out name))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!IntInputBox.Show(HaRepackerLib.Properties.Resources.MainAddShort, out name, out value))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameValueInputBox.Show(HaRepackerLib.Properties.Resources.MainAddLink, out name, out value))
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
                Warning.Error(HaRepackerLib.Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!VectorInputBox.Show(HaRepackerLib.Properties.Resources.MainAddVec, out name, out pt))
                return;
            ((WzNode)target).AddObject(new WzVectorProperty(name, new WzIntProperty("X", ((Point)pt).X), new WzIntProperty("Y", ((Point)pt).Y)), UndoRedoMan);
        }

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
        #endregion

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

        private bool yesToAll = false;
        private bool noToAll = false;

        private bool ShowReplaceDialog(string name)
        {
            if (yesToAll) return true;
            else if (noToAll) return false;
            else
            {
                ReplaceBox dialog = new ReplaceBox(name);
                dialog.ShowDialog();
                switch (dialog.result)
                {
                    case ReplaceResult.NoToAll:
                        noToAll = true;
                        return false;
                    case ReplaceResult.No:
                        return false;
                    case ReplaceResult.YesToAll:
                        yesToAll = true;
                        return true;
                    case ReplaceResult.Yes:
                        return true;
                }
            }
            throw new Exception("cant get here anyway");
        }

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
        ///  Remove all WZ image resource to optimize for botting purposes
        /// </summary>
        public void DoRemoveImageResource()
        {
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                WzObject wzObj = (WzObject)node.Tag;// CloneWzObject((WzObject)node.Tag);

            }
        }

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

        public void DoPaste()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmPaste))
                return;

            yesToAll = false;
            noToAll = false;
            WzNode parent = (WzNode)DataTree.SelectedNode;
            WzObject parentObj = (WzObject)parent.Tag;

            if (parent != null && parent.Tag is WzImage && parent.Nodes.Count == 0)
            {
                ParseOnDataTreeSelectedItem(parent);
            }

            if (parentObj is WzFile)
                parentObj = ((WzFile)parentObj).WzDirectory;

            foreach (WzObject obj in clipboard)
            {
                if (((obj is WzDirectory || obj is WzImage) && parentObj is WzDirectory) || (obj is WzImageProperty && parentObj is IPropertyContainer))
                {
                    WzObject clone = CloneWzObject(obj);
                    if (clone == null)
                        continue;
                    WzNode node = new WzNode(clone);
                    WzNode child = WzNode.GetChildNode(parent, node.Text);
                    if (child != null)
                    {
                        if (ShowReplaceDialog(node.Text))
                            child.Delete();
                        else return;
                    }
                    parent.AddNode(node);

                }
            }
        }

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
            searchValues = UserSettings.SearchStringValues;
            currentidx = 0;
            searchText = findBox.Text;
            extractImages = UserSettings.ParseImagesInSearch;
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
            searchValues = UserSettings.SearchStringValues;
            currentidx = 0;
            searchText = findBox.Text;
            extractImages = UserSettings.ParseImagesInSearch;
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
