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
    public class ObjectInfo : MapleDrawableInfo
    {
        private string _oS;
        private string _l0;
        private string _l1;
        private string _l2;
        private List<List<XNA.Point>> footholdOffsets = null;
        private List<List<XNA.Point>> ropeOffsets = null;
        private List<List<XNA.Point>> ladderOffsets = null;
        private List<XNA.Point> chairOffsets = null;
        private bool connect;

        public ObjectInfo(Bitmap image, System.Drawing.Point origin, string oS, string l0, string l1, string l2, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            this._oS = oS;
            this._l0 = l0;
            this._l1 = l1;
            this._l2 = l2;
            this.connect = oS.StartsWith("connect");
        }

        public static ObjectInfo Get(string oS, string l0, string l1, string l2)
        {
            WzImageProperty objInfoProp = Program.InfoManager.ObjectSets[oS][l0][l1][l2];
            if (objInfoProp.HCTag == null)
                objInfoProp.HCTag = ObjectInfo.Load((WzSubProperty)objInfoProp, oS, l0, l1, l2);
            return (ObjectInfo)objInfoProp.HCTag;
        }

        private static List<XNA.Point> ParsePropToOffsetList(WzImageProperty prop)
        {
            List<XNA.Point> result = new List<XNA.Point>();
            foreach (WzVectorProperty point in prop.WzProperties)
            {
                result.Add(WzInfoTools.VectorToXNAPoint(point));
            }
            return result;
        }

        private static List<List<XNA.Point>> ParsePropToOffsetMap(WzImageProperty prop)
        {
            if (prop == null)
                return null;
            List<List<XNA.Point>> result = new List<List<XNA.Point>>();
            if (prop is WzConvexProperty)
            {
                result.Add(ParsePropToOffsetList((WzConvexProperty)prop));
            }
            else if (prop is WzSubProperty)
            {
                foreach (WzConvexProperty offsetSet in prop.WzProperties)
                {
                    result.Add(ParsePropToOffsetList(offsetSet));
                }
            }
            else
            {
                result = null;
            }
            return result;
        }

        private static ObjectInfo Load(WzSubProperty parentObject, string oS, string l0, string l1, string l2)
        {
            WzCanvasProperty frame1 = (WzCanvasProperty)WzInfoTools.GetRealProperty(parentObject["0"]);
            ObjectInfo result = new ObjectInfo(frame1.PngProperty.GetPNG(false), WzInfoTools.VectorToSystemPoint((WzVectorProperty)frame1["origin"]), oS, l0, l1, l2, parentObject);
            WzImageProperty chairs = parentObject["seat"];
            WzImageProperty ropes = frame1["rope"];
            WzImageProperty ladders = frame1["ladder"];
            WzImageProperty footholds = frame1["foothold"];
            result.footholdOffsets = ParsePropToOffsetMap(footholds);
            result.ropeOffsets = ParsePropToOffsetMap(ropes);
            result.ladderOffsets = ParsePropToOffsetMap(ladders);
            if (chairs != null)
                result.chairOffsets = ParsePropToOffsetList(chairs);
            return result;
        }

        private void CreateFootholdsFromAnchorList(Board board, List<FootholdAnchor> anchors)
        {
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                FootholdLine fh = new FootholdLine(board, anchors[i], anchors[i + 1]);
                board.BoardItems.FootholdLines.Add(fh);
            }
        }

        public void ParseOffsets(ObjectInstance instance, Board board, int x, int y)
        {
            bool ladder = l0 == "ladder";
            if (footholdOffsets != null)
            {
                foreach (List<XNA.Point> anchorList in footholdOffsets)
                {
                    List<FootholdAnchor> anchors = new List<FootholdAnchor>();
                    foreach (XNA.Point foothold in anchorList)
                    {
                        FootholdAnchor anchor = new FootholdAnchor(board, x + foothold.X, y + foothold.Y, instance.LayerNumber, instance.PlatformNumber, true);
                        board.BoardItems.FHAnchors.Add(anchor);
                        instance.BindItem(anchor, foothold);
                        anchors.Add(anchor);
                    }
                    CreateFootholdsFromAnchorList(board, anchors);
                }
            }
            if (chairOffsets != null)
            {
                foreach (XNA.Point chairPos in chairOffsets)
                {
                    Chair chair = new Chair(board, x + chairPos.X, y + chairPos.Y);
                    board.BoardItems.Chairs.Add(chair);
                    instance.BindItem(chair, chairPos);
                }
            }
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            ObjectInstance instance = new ObjectInstance(this, layer, board, x, y, z, layer.zMDefault, false, false, false, false, null, null, null, null, null, null, null, flip);
            ParseOffsets(instance, board, x, y);
            return instance;
        }

        public BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, int zM, MapleBool r, MapleBool hide, MapleBool reactor, MapleBool flow, int? rx, int? ry, int? cx, int? cy, string name, string tags, List<ObjectInstanceQuest> questInfo, bool flip, bool parseOffsets)
        {
            ObjectInstance instance = new ObjectInstance(this, layer, board, x, y, z, zM, r, hide, reactor, flow, rx, ry, cx, cy, name, tags, questInfo, flip);
            if (parseOffsets) ParseOffsets(instance, board, x, y);
            return instance;
        }

        public string oS
        {
            get
            {
                return _oS;
            }
            set
            {
                this._oS = value;
            }
        }

        public string l0
        {
            get
            {
                return _l0;
            }
            set
            {
                this._l0 = value;
            }
        }

        public string l1
        {
            get
            {
                return _l1;
            }
            set
            {
                this._l1 = value;
            }
        }

        public string l2
        {
            get
            {
                return _l2;
            }
            set
            {
                this._l2 = value;
            }
        }

        public List<List<XNA.Point>> FootholdOffsets
        {
            get
            {
                return footholdOffsets;
            }
        }

        public List<XNA.Point> ChairOffsets
        {
            get
            {
                return chairOffsets;
            }
        }

        public List<List<XNA.Point>> RopeOffsets
        {
            get
            {
                return ropeOffsets;
            }
        }

        public List<List<XNA.Point>> LadderOffsets
        {
            get
            {
                return ladderOffsets;
            }
        }

        public bool Connect { get { return connect; } }
    }

}
