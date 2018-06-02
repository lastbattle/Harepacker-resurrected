/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using HaCreator.MapEditor;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using System.Collections;
using HaCreator.ThirdParty;
using HaCreator.GUI;
using MapleLib.WzLib.WzStructure;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.UndoRedo;

namespace HaCreator.GUI.EditorPanels
{
    public partial class TilePanel : DockContent
    {
        private HaCreatorStateManager hcsm;

        public TilePanel(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            hcsm.SetTilePanel(this);
            InitializeComponent();

            List<string> sortedTileSets = new List<string>();
            foreach (KeyValuePair<string, WzImage> tS in Program.InfoManager.TileSets)
                sortedTileSets.Add(tS.Key);
            sortedTileSets.Sort();
            foreach (string tS in sortedTileSets)
                tileSetList.Items.Add(tS);
        }

        private void searchResultsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedIndexChanged.Invoke(sender, e);
        }

        public event EventHandler SelectedIndexChanged;

        private void tileBrowse_Click(object sender, EventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                new TileSetBrowser(tileSetList).ShowDialog();
            }
        }

        private void tileSetList_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadTileSetList();
        }

        public void LoadTileSetList()
        {
            lock (hcsm.MultiBoard)
            {
                if (tileSetList.SelectedItem == null) return;
                tileImagesContainer.Controls.Clear();
                string selectedSetName = (string)tileSetList.SelectedItem;
                if (!Program.InfoManager.TileSets.ContainsKey(selectedSetName))
                    return;
                WzImage tileSetImage = Program.InfoManager.TileSets[selectedSetName];
                int? mag = InfoTool.GetOptionalInt(tileSetImage["info"]["mag"]);
                foreach (WzSubProperty tCat in tileSetImage.WzProperties)
                {
                    if (tCat.Name == "info") continue;
                    if (ApplicationSettings.randomTiles)
                    {
                        WzCanvasProperty canvasProp = (WzCanvasProperty)tCat["0"];
                        if (canvasProp == null) continue;
                        ImageViewer item = tileImagesContainer.Add(canvasProp.PngProperty.GetPNG(false), tCat.Name, true);
                        TileInfo[] randomInfos = new TileInfo[tCat.WzProperties.Count];
                        for (int i = 0; i < randomInfos.Length; i++)
                        {
                            randomInfos[i] = TileInfo.Get((string)tileSetList.SelectedItem, tCat.Name, tCat.WzProperties[i].Name, mag);
                        }
                        item.Tag = randomInfos;
                        item.MouseDown += new MouseEventHandler(tileItem_Click);
                        item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                    }
                    else
                    {
                        foreach (WzCanvasProperty tile in tCat.WzProperties)
                        {
                            ImageViewer item = tileImagesContainer.Add(tile.PngProperty.GetPNG(false), tCat.Name + "/" + tile.Name, true);
                            item.Tag = TileInfo.Get((string)tileSetList.SelectedItem, tCat.Name, tile.Name, mag);
                            item.MouseDown += new MouseEventHandler(tileItem_Click);
                            item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                        }
                    }
                }
            }
        }

        void tileItem_Click(object sender, MouseEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                ImageViewer item = (ImageViewer)sender;
                if (!hcsm.MultiBoard.AssertLayerSelected())
                {
                    return;
                }
                Layer layer = hcsm.MultiBoard.SelectedBoard.SelectedLayer;
                if (layer.tS != null)
                {
                    TileInfo infoToAdd = null;
                    if (ApplicationSettings.randomTiles)
                        infoToAdd = ((TileInfo[])item.Tag)[0];
                    else
                        infoToAdd = (TileInfo)item.Tag;
                    if (infoToAdd.tS != layer.tS)
                    {
                        if (MessageBox.Show("This action will change the layer's tS. Proceed?", "Layer tS Change", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != System.Windows.Forms.DialogResult.Yes)
                            return;
                        List<UndoRedoAction> actions = new List<UndoRedoAction>();
                        actions.Add(UndoRedoManager.LayerTSChanged(layer, layer.tS, infoToAdd.tS));
                        layer.ReplaceTS(infoToAdd.tS);
                        hcsm.MultiBoard.SelectedBoard.UndoRedoMan.AddUndoBatch(actions);
                    }
                }
                hcsm.EnterEditMode(ItemTypes.Tiles);
                if (ApplicationSettings.randomTiles)
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetRandomTilesMode((TileInfo[])item.Tag);
                else
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((TileInfo)item.Tag);
                hcsm.MultiBoard.Focus();
                item.IsActive = true;
            }
        }
    }
}
