/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor
{
    public class Layer
    {
        private List<LayeredItem> items = new List<LayeredItem>(); //needed?
        private SortedSet<int> zms = new SortedSet<int>();
        private int num;
        private Board board;
        private string _tS = null;

        public Layer(Board board)
        {
            this.board = board;
            if (board.Layers.Count == 10) throw new NotSupportedException("Cannot add more than 10 layers (why would you need that much anyway?)");
            num = board.Layers.Count;
            board.Layers.Add(this);
        }

        public List<LayeredItem> Items
        {
            get
            {
                return items;
            }
            set
            {
                items = value;
            }
        }

        public int LayerNumber
        {
            get
            {
                return num;
            }
        }

        public string tS
        {
            get { return _tS; }
            set 
            {
                lock (board.ParentControl)
                {
                    if (_tS != value)
                    {
                        _tS = value;
                        if (!board.Loading)
                        {
                            board.ParentControl.LayerTSChanged(this);
                        }
                    }
                }
            }
        }

        public void ReplaceTS(string newTS)
        {
            lock (board.ParentControl)
            {
                foreach (LayeredItem item in items)
                {
                    if (item is TileInstance)
                    {
                        TileInstance tile = (TileInstance)item;
                        TileInfo tileBase = (TileInfo)tile.BaseInfo;
                        TileInfo tileInfo = TileInfo.GetWithDefaultNo(newTS, tileBase.u, tileBase.no, "0");
                        tile.SetBaseInfo(tileInfo);
                    }
                }
            }
            this.tS = newTS;
        }

        public static Layer GetLayerByNum(Board board, int num)
        {
            return board.Layers[num];
        }

        public void RecheckTileSet()
        {
            foreach (LayeredItem item in items)
                if (item is TileInstance)
                {
                    tS = ((TileInfo)item.BaseInfo).tS;
                    return;
                }
            tS = null;
        }

        public void RecheckZM()
        {
            zMList.Clear();
            foreach (LayeredItem li in items)
            {
                zMList.Add(li.PlatformNumber);
            }
        }

        public int zMDefault { get { return board.SelectedPlatform == -1 ? zMList.ElementAt(0) : board.SelectedPlatform; } }

        public SortedSet<int> zMList { get { return zms; } }

        public override string ToString()
        {
            return LayerNumber.ToString() + (tS != null ? (" - " + tS) : "");
        }
    }
}
