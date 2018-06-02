/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance
{
    public abstract class LayeredItem : BoardItem, IContainsLayerInfo, ISerializable
    {
        private Layer layer;
        private int zm;

        public LayeredItem(Board board, Layer layer, int zm, int x, int y, int z)
            : base(board, x, y, z)
        {
            this.layer = layer;
            layer.Items.Add(this);
            this.zm = zm;
        }

        public override void RemoveItem(List<UndoRedoAction> undoPipe)
        {
            lock (board.ParentControl)
            {
                layer.Items.Remove(this);
                base.RemoveItem(undoPipe);
            }
        }

        public override void InsertItem()
        {
            lock (board.ParentControl)
            {
                layer.Items.Add(this);
                base.InsertItem();
            }
        }

        public Layer Layer
        {
            get
            {
                return layer;
            }
            set
            {
                lock (board.ParentControl)
                {
                    layer.Items.Remove(this);
                    layer = value;
                    layer.Items.Add(this);
                    Board.BoardItems.Sort();
                }
            }
        }

        public int LayerNumber
        {
            get { return Layer.LayerNumber; }
            set
            {
                lock (board.ParentControl)
                {
                    Layer = Board.Layers[value];
                }
            }
        }

        public override bool CheckIfLayerSelected(SelectionInfo sel)
        {
            return (sel.selectedLayer == -1 || Layer.LayerNumber == sel.selectedLayer) && (sel.selectedPlatform == -1 || PlatformNumber == sel.selectedPlatform);
        }

        public int PlatformNumber { get { return zm; } set { zm = value; } }

        public new class SerializationForm : BoardItem.SerializationForm
        {
            public int layer, zm;
        }

        public override object Serialize()
        {
            SerializationForm result = new SerializationForm();
            UpdateSerializedForm(result);
            return result;
        }

        protected void UpdateSerializedForm(SerializationForm result)
        {
            base.UpdateSerializedForm(result);
            result.layer = layer.LayerNumber;
            result.zm = zm;
        }

        public LayeredItem(Board board, SerializationForm json)
            : base(board, json)
        {
            // Layer and zM will not be retained upon copying and pasting, since AddToBoard will set layer & zm itself
            // This feels like the more expected behavior to me. This also simplifies tile copying.
            // If the item is deserialized as part of crash recoverty, AddToBoard will not set the layer & zm, and we will recover correctly
            layer = board.Layers[json.layer];
            zm = json.zm;
        }

        public override void AddToBoard(List<UndoRedoAction> undoPipe)
        {
            base.AddToBoard(undoPipe);
            if (undoPipe != null)
            {
                layer = board.SelectedLayer;
                zm = board.SelectedPlatform;
            }
            layer.Items.Add(this);
        }
    }
}
