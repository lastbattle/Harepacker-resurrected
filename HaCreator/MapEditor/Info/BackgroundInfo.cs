/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Info
{
    public class BackgroundInfo : MapleDrawableInfo
    {
        private string _bS;
        private bool _ani;
        private string _no;

        public BackgroundInfo(Bitmap image, System.Drawing.Point origin, string bS, bool ani, string no, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            _bS = bS;
            _ani = ani;
            _no = no;
        }

        public static BackgroundInfo Get(string bS, bool ani, string no)
        {
            if (!Program.InfoManager.BackgroundSets.ContainsKey(bS))
                return null;
            WzImage bsImg = Program.InfoManager.BackgroundSets[bS];
            WzImageProperty bgInfoProp = bsImg[ani ? "ani" : "back"][no];
            if (bgInfoProp.HCTag == null)
                bgInfoProp.HCTag = BackgroundInfo.Load(bgInfoProp, bS, ani, no);
            return (BackgroundInfo)bgInfoProp.HCTag;
        }

        private static BackgroundInfo Load(WzImageProperty parentObject, string bS, bool ani, string no)
        {
            WzCanvasProperty frame0 = ani ? (WzCanvasProperty)WzInfoTools.GetRealProperty(parentObject["0"]) : (WzCanvasProperty)WzInfoTools.GetRealProperty(parentObject);
            return new BackgroundInfo(frame0.PngProperty.GetPNG(false), WzInfoTools.VectorToSystemPoint((WzVectorProperty)frame0["origin"]), bS, ani, no, parentObject);
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            return new BackgroundInstance(this, board, x, y, z, -100, -100, 0, 0, 0, 255, false, flip);
        }

        public BoardItem CreateInstance(Board board, int x, int y, int z, int rx, int ry, int cx, int cy, BackgroundType type, int a, bool front, bool flip)
        {
            return new BackgroundInstance(this, board, x, y, z, rx, ry, cx, cy, type, a, front, flip);
        }

        public string bS
        {
            get
            {
                return _bS;
            }
            set
            {
                this._bS = value;
            }
        }

        public bool ani
        {
            get
            {
                return _ani;
            }
            set
            {
                this._ani = value;
            }
        }

        public string no
        {
            get
            {
                return _no;
            }
            set
            {
                this._no = value;
            }
        }
    }
}
