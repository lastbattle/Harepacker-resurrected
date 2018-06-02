/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class LayerChange : EditorBase
    {
        List<BoardItem> items;
        Board board;

        public LayerChange(List<BoardItem> items, Board board)
        {
            this.items = items;
            this.board = board;
            InitializeComponent();
            
            board.Layers.ForEach(x => layerBox.Items.Add(x.ToString()));
            if (board.SelectedLayerIndex == -1)
            {
                layerBox.SelectedIndex = 0;
                zmBox.SelectedIndex = 0;
            }
            else
            {
                layerBox.SelectedIndex = board.SelectedLayerIndex;
                if (board.SelectedPlatform != -1)
                {
                    zmBox.SelectedItem = board.SelectedPlatform;
                }
            }
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }


        private bool LayeredItemsSelected(out int layer)
        {
            foreach (BoardItem item in board.SelectedItems)
                if (item is LayeredItem)
                {
                    layer = ((LayeredItem)item).Layer.LayerNumber;
                    return true;
                }
            layer = 0;
            return false;
        }

        private bool LayerCapableOfHoldingSelectedItems(Layer layer)
        {
            if (layer.tS == null) return true;
            foreach (BoardItem item in items)
                if (item is TileInstance && ((TileInfo)item.BaseInfo).tS != layer.tS) return false;
            return true;
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            Layer targetLayer = board.Layers[layerBox.SelectedIndex];
            int zm = (int)zmBox.SelectedItem;
            if (!LayerCapableOfHoldingSelectedItems(targetLayer))
            {
                MessageBox.Show("Error: Target layer cannot hold the selected items because they contain tiles with a tS different from the layer's", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            List<UndoRedoAction> actions = new List<UndoRedoAction>();
            HashSet<int> touchedLayers = new HashSet<int>();
            foreach (BoardItem item in items)
            {
                if (!(item is IContainsLayerInfo)) continue;
                IContainsLayerInfo li = (IContainsLayerInfo)item;
                int oldLayer = li.LayerNumber;
                int oldZm = li.PlatformNumber;
                touchedLayers.Add(oldLayer);
                li.LayerNumber = targetLayer.LayerNumber;
                li.PlatformNumber = zm;
                actions.Add(UndoRedoManager.ItemLayerPlatChanged(li, new Tuple<int,int>(oldLayer, oldZm), new Tuple<int,int>(li.LayerNumber, li.PlatformNumber)));
            }
            if (actions.Count > 0)
                board.UndoRedoMan.AddUndoBatch(actions);
            touchedLayers.ToList().ForEach(x => board.Layers[x].RecheckTileSet());
            targetLayer.RecheckTileSet();
            InputHandler.ClearSelectedItems(board);
            Close();
        }

        private void layerBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            zmBox.Items.Clear();
            board.Layers[layerBox.SelectedIndex].zMList.ToList().ForEach(x => zmBox.Items.Add(x));
            zmBox.SelectedIndex = 0;
        }
    }
}
