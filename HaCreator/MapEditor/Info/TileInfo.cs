/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Info
{
    public class TileInfo : MapleDrawableInfo
    {
        private string _tS;
        private string _u;
        private string _no;
        private int _mag;
        private int _z;
        private List<XNA.Point> footholdOffsets = new List<XNA.Point>();

        public TileInfo(Bitmap image, System.Drawing.Point origin, string tS, string u, string no, int mag, int z, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            this._tS = tS;
            this._u = u;
            this._no = no;
            this._mag = mag;
            this._z = z;
        }

        public static TileInfo Get(string tS, string u, string no)
        {
            int? mag = InfoTool.GetOptionalInt(Program.InfoManager.TileSets[tS]["info"]["mag"]);
            return Get(tS, u, no, mag);
        }

        public static TileInfo GetWithDefaultNo(string tS, string u, string no, string defaultNo)
        {
            int? mag = InfoTool.GetOptionalInt(Program.InfoManager.TileSets[tS]["info"]["mag"]);
            WzImageProperty prop = Program.InfoManager.TileSets[tS][u];
            WzImageProperty tileInfoProp = prop[no];
            if (tileInfoProp == null)
            {
                tileInfoProp = prop[defaultNo];
            }
            if (tileInfoProp.HCTag == null)
                tileInfoProp.HCTag = TileInfo.Load((WzCanvasProperty)tileInfoProp, tS, u, no, mag);
            return (TileInfo)tileInfoProp.HCTag;
        }

        // Optimized version, for cases where you already know the mag (e.g. mass loading tiles of the same tileSet)
        public static TileInfo Get(string tS, string u, string no, int? mag)
        {
            WzImageProperty tileInfoProp = Program.InfoManager.TileSets[tS][u][no];
            if (tileInfoProp.HCTag == null)
                tileInfoProp.HCTag = TileInfo.Load((WzCanvasProperty)tileInfoProp, tS, u, no, mag);
            return (TileInfo)tileInfoProp.HCTag;
        }

        private static TileInfo Load(WzCanvasProperty parentObject, string tS, string u, string no, int? mag)
        {
            WzImageProperty zProp = parentObject["z"];
            int z = zProp == null ? 0 : InfoTool.GetInt(zProp);
            TileInfo result = new TileInfo(parentObject.PngProperty.GetPNG(false), WzInfoTools.VectorToSystemPoint((WzVectorProperty)parentObject["origin"]), tS, u, no, mag.HasValue ? mag.Value : 1, z, parentObject);
            WzConvexProperty footholds = (WzConvexProperty)parentObject["foothold"];
            if (footholds != null)
                foreach (WzVectorProperty foothold in footholds.WzProperties)
                    result.footholdOffsets.Add(WzInfoTools.VectorToXNAPoint(foothold));
            if (UserSettings.FixFootholdMispositions)
            {
                FixFootholdMispositions(result);
            }
            return result;
        }

        /* The sole reason behind this function's existence is that Nexon's designers are a bunch of incompetent goons.

         * In a nutshell, some tiles (mostly old ones) have innate footholds that do not overlap when snapping them to each other, causing a weird foothold structure.
         * I do not know how Nexon's editor overcame this; I am assuming they manually connected the footholds to sort that out. However, since HaCreator only allows automatic
         * connection of footholds, we need to sort these cases out preemptively here.
        */
        private static void FixFootholdMispositions(TileInfo result)
        {
            switch (result.u)
            {
                case "enV0":
                    MoveFootholdY(result, true, false, 60);
                    MoveFootholdY(result, false, true, 60);
                    break;
                case "enV1":
                    MoveFootholdY(result, true, true, 60);
                    MoveFootholdY(result, false, false, 60);
                    break;
                case "enH0":
                    MoveFootholdX(result, true, true, 90);
                    MoveFootholdX(result, false, false, 90);
                    break;
                case "slLU":
                    MoveFootholdX(result, true, false, -90);
                    MoveFootholdX(result, false, true, -90);
                    break;
                case "slRU":
                    MoveFootholdX(result, true, true, 90);
                    MoveFootholdX(result, false, false, 90);
                    break;
                case "edU":
                    MoveFootholdY(result, true, false, 0);
                    MoveFootholdY(result, false, false, 0);
                    break;
            }
        }

        private static void MoveFootholdY(TileInfo result, bool first, bool top, int height)
        {
            if (result.footholdOffsets.Count < 1)
                return;
            int idx = first ? 0 : result.footholdOffsets.Count - 1;
            int y = top ? 0 : (height * result.mag);
            if (result.footholdOffsets[idx].Y != y)
            {
                result.footholdOffsets[idx] = new XNA.Point(result.footholdOffsets[idx].X, y);
            }
        }

        private static void MoveFootholdX(TileInfo result, bool first, bool left, int width)
        {
            if (result.footholdOffsets.Count < 1)
                return;
            int idx = first ? 0 : result.footholdOffsets.Count - 1;
            int x = left ? 0 : (width * result.mag);
            if (result.footholdOffsets[idx].X != x)
            {
                result.footholdOffsets[idx] = new XNA.Point(x, result.footholdOffsets[idx].Y);
            }
        }

        public void ParseOffsets(TileInstance instance, Board board, int x, int y)
        {
            List<FootholdAnchor> anchors = new List<FootholdAnchor>();
            foreach (XNA.Point foothold in footholdOffsets)
            {
                FootholdAnchor anchor = new FootholdAnchor(board, x + foothold.X, y + foothold.Y, instance.LayerNumber, instance.PlatformNumber, true);
                anchors.Add(anchor);
                board.BoardItems.FHAnchors.Add(anchor);
                instance.BindItem(anchor, foothold);
            }
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                FootholdLine fh = new FootholdLine(board, anchors[i], anchors[i + 1]);
                board.BoardItems.FootholdLines.Add(fh);
            }
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            TileInstance instance = new TileInstance(this, layer, board, x, y, z, layer.zMDefault);
            ParseOffsets(instance, board, x, y);
            return instance;
        }

        public BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, int zM, bool flip, bool parseOffsets)
        {
            TileInstance instance = new TileInstance(this, layer, board, x, y, z, zM);
            if (parseOffsets) ParseOffsets(instance, board, x, y);
            return instance;
        }

        public string tS
        {
            get
            {
                return _tS;
            }
            set
            {
                this._tS = value;
            }
        }

        public string u
        {
            get
            {
                return _u;
            }
            set
            {
                this._u = value;
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

        public int mag
        {
            get { return _mag; }
            set { _mag = value; }
        }

        public List<XNA.Point> FootholdOffsets
        {
            get
            {
                return footholdOffsets;
            }
        }

        public int z { get { return _z; } set { _z = value; } }
    }
}
