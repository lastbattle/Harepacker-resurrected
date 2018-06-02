/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class FootholdAnchor : MapleDot, IContainsLayerInfo, ISerializable
    {
        private int layer;
        private int zm;

        public bool user;

        public FootholdAnchor(Board board, int x, int y, int layer, int zm, bool user)
            : base(board, x, y)
        {
            this.layer = layer;
            this.zm = zm;
            this.user = user;
        }

        public override bool CheckIfLayerSelected(SelectionInfo sel)
        {
            return (sel.selectedLayer == -1 || sel.selectedLayer == layer) && (sel.selectedPlatform == -1 || sel.selectedPlatform == zm);
        }

        public override XNA.Color Color
        {
            get
            {
                return UserSettings.FootholdColor;
            }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.FootholdInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Footholds; }
        }

        protected override bool RemoveConnectedLines
        {
            get { return true; }
        }

        public static int FHAnchorSorter(FootholdAnchor c, FootholdAnchor d)
        {
            if (c.X > d.X)
                return 1;
            else if (c.X < d.X)
                return -1;
            else
            {
                if (c.Y > d.Y)
                    return 1;
                else if (c.Y < d.Y)
                    return -1;
                else
                {
                    if (c.LayerNumber > d.LayerNumber)
                        return 1;
                    else if (c.LayerNumber < d.LayerNumber)
                        return -1;
                    else
                    {
                        if (c.PlatformNumber > d.PlatformNumber)
                            return 1;
                        else if (c.PlatformNumber < d.PlatformNumber)
                            return -1;
                        else
                        {
                            if (c.Parent != null && c.Parent is TileInstance && ((TileInfo)c.Parent.BaseInfo).u == "edU")
                                return -1;
                            else if (d.Parent != null && d.Parent is TileInstance && ((TileInfo)d.Parent.BaseInfo).u == "edU")
                                return 1;
                            else
                                return 0;
                        }
                    }
                }
            }
        }

        public static void MergeAnchors(FootholdAnchor a, FootholdAnchor b)
        {
            foreach (FootholdLine line in b.connectedLines)
            {
                if (line.FirstDot == b)
                    line.FirstDot = a;
                else if (line.SecondDot == b)
                    line.SecondDot = a;
                else
                    throw new Exception("No anchor matches foothold");

                a.connectedLines.Add(line);
            }
            b.connectedLines.Clear();
        }

        public bool AllConnectedLinesVertical()
        {
            foreach (MapleLine line in connectedLines)
            {
                if (line.FirstDot.X != line.SecondDot.X)
                {
                    return false;
                }
            }
            return true;
        }
        public bool AllConnectedLinesHorizontal()
        {
            foreach (MapleLine line in connectedLines)
            {
                if (line.FirstDot.Y != line.SecondDot.Y)
                {
                    return false;
                }
            }
            return true;
        }

        public int LayerNumber
        {
            get { return layer; }
            set { layer = value; }
        }

        public int PlatformNumber
        {
            get { return zm; }
            set { zm = value; }
        }

        public FootholdLine GetOtherLine(FootholdLine line)
        {
            foreach (FootholdLine currLine in connectedLines)
            {
                if (line != currLine)
                {
                    return currLine;
                }
            }
            return null;
        }

        public new class SerializationForm : BoardItem.SerializationForm
        {
            public int layer, zm;
            public bool user;
        }

        public override bool ShouldSelectSerialized
        {
            get
            {
                return true;
            }
        }

        public override List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems)
        {
            List<ISerializableSelector> serList = new List<ISerializableSelector>();
            foreach (FootholdLine fh in connectedLines)
            {
                if (serializedItems.Contains(fh.GetOtherAnchor(this)))
                    serList.Add(fh);
            }
            return serList;
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
            result.layer = layer;
            result.zm = zm;
            result.user = user;
        }

        public override IDictionary<string, object> SerializeBindings(Dictionary<ISerializable, long> refDict)
        {
            // No bindings
            return null;
        }

        public FootholdAnchor(Board board, SerializationForm json)
            : base(board, json)
        {
            layer = json.layer;
            zm = json.zm;
            user = json.user; // Will be reset to true on AddToBoard if we are copypasting
        }

        public override void DeserializeBindings(IDictionary<string, object> bindSer, Dictionary<long, ISerializable> refDict)
        {
            // No bindings
            return;
        }

        public override void AddToBoard(List<UndoRedo.UndoRedoAction> undoPipe)
        {
            if (undoPipe != null)
            {
                user = true;
                layer = board.SelectedLayerIndex;
                zm = board.SelectedPlatform;
            }
            base.AddToBoard(undoPipe);
        }
    }
}
