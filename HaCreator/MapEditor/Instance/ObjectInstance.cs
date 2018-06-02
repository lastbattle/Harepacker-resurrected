/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Input;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance
{
    public class ObjectInstance : LayeredItem, IFlippable, ISnappable
    {
        private ObjectInfo baseInfo;
        private bool flip;
        private MapleBool _r;
        private string name;
        private MapleBool _hide;
        private MapleBool _reactor;
        private MapleBool _flow;
        private int? _rx, _ry, _cx, _cy;
        private string _tags;
        private List<ObjectInstanceQuest> questInfo;

        public ObjectInstance(ObjectInfo baseInfo, Layer layer, Board board, int x, int y, int z, int zM, MapleBool r, MapleBool hide, MapleBool reactor, MapleBool flow, int? rx, int? ry, int? cx, int? cy, string name, string tags, List<ObjectInstanceQuest> questInfo, bool flip)
            : base(board, layer, zM, x, y, z)
        {
            this.baseInfo = baseInfo;
            this.flip = flip;
            this._r = r;
            this.name = name;
            this._hide = hide;
            this._reactor = reactor;
            this._flow = flow;
            this._rx = rx;
            this._ry = ry;
            this._cx = cx;
            this._cy = cy;
            this._tags = tags;
            this.questInfo = questInfo;
            if (flip)
                X -= Width - 2 * Origin.X;
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Objects; }
        }

        public override MapleDrawableInfo BaseInfo
        {
            get { return baseInfo; }
        }

        public override XNA.Color GetColor(SelectionInfo sel, bool selected)
        {
            XNA.Color c = base.GetColor(sel, selected);
            if (_hide) c.R = (byte)UserSettings.HiddenLifeR;
            return c;
        }

        public bool Flip
        {
            get
            {
                return flip;
            }
            set
            {
                if (flip == value) return;
                flip = value;
                int xFlipShift = Width - 2 * Origin.X;
                if (flip) X -= xFlipShift;
                else X += xFlipShift;
            }
        }

        public int UnflippedX
        {
            get
            {
                return flip ? (X + Width - 2 * Origin.X) : X;
            }
        }

        private void DrawOffsetMap(SpriteBatch sprite, List<List<XNA.Point>> offsetMap, int xBase, int yBase)
        {
            foreach (List<XNA.Point> offsetList in offsetMap)
            {
                foreach (XNA.Point offset in offsetList)
                {
                    Board.ParentControl.DrawDot(sprite, xBase + offset.X, yBase + offset.Y, MultiBoard.RopeInactiveColor, 1);
                }
            }
        }

        public override void Draw(SpriteBatch sprite, XNA.Color color, int xShift, int yShift)
        {
            XNA.Rectangle destinationRectangle = new XNA.Rectangle((int)X + xShift - Origin.X, (int)Y + yShift - Origin.Y, Width, Height);
            sprite.Draw(baseInfo.GetTexture(sprite), destinationRectangle, null, color, 0f, new XNA.Vector2(0, 0), Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0 /*Layer.LayerNumber / 10f + Z / 1000f*/);
            if (ApplicationSettings.InfoMode)
            {
                int xBase = (int)X + xShift;
                int yBase = (int)Y + yShift;
                ObjectInfo oi = (ObjectInfo)baseInfo;
                if (oi.RopeOffsets != null)
                    DrawOffsetMap(sprite, oi.RopeOffsets, xBase, yBase);
                if (oi.LadderOffsets != null)
                    DrawOffsetMap(sprite, oi.LadderOffsets, xBase, yBase);
            }
            base.Draw(sprite, color, xShift, yShift);
        }

        public override System.Drawing.Bitmap Image
        {
            get
            {
                return baseInfo.Image;
            }
        }

        public override int Width
        {
            get { return baseInfo.Width; }
        }

        public override int Height
        {
            get { return baseInfo.Height; }
        }

        public override System.Drawing.Point Origin
        {
            get
            {
                return baseInfo.Origin;
            }
        }

        public void DoSnap()
        {
            if (!baseInfo.Connect)
                return;
            XNA.Point? closestDestPoint = null;
            double closestDistance = double.MaxValue;
            foreach (LayeredItem li in Board.BoardItems.TileObjs)
            {
                // Trying to snap to other selected items can mess up some of the mouse bindings
                if (!(li is ObjectInstance) || li.Selected || li.Equals(this))
                    continue;
                ObjectInstance objInst = (ObjectInstance)li;
                ObjectInfo objInfo = (ObjectInfo)objInst.BaseInfo;
                if (!objInfo.Connect)
                    continue;
                XNA.Point snapPoint = new XNA.Point(objInst.X, objInst.Y - objInst.Origin.Y + objInst.Height + this.Origin.Y);
                double dx = snapPoint.X - X;
                double dy = snapPoint.Y - Y;
                if (dx > UserSettings.SnapDistance || dy > UserSettings.SnapDistance)
                    continue;
                double distance = InputHandler.Distance(dx, dy);
                if (distance > UserSettings.SnapDistance)
                    continue;
                if (closestDistance > distance)
                {
                    closestDistance = distance;
                    closestDestPoint = snapPoint;
                }
            }

            if (closestDestPoint.HasValue)
            {
                SnapMoveAllMouseBoundItems(new XNA.Point(closestDestPoint.Value.X, closestDestPoint.Value.Y));
            }
        }

        public string Name { get { return name; } set { name = value; } }
        public string tags { get { return _tags; } set { _tags = value; } }
        public MapleBool r { get { return _r; } set { _r = value; } }
        public MapleBool hide { get { return _hide; } set { _hide = value; } }
        public MapleBool flow { get { return _flow; } set { _flow = value; } }
        public MapleBool reactor { get { return _reactor; } set { _reactor = value; } }
        public int? rx { get { return _rx; } set { _rx = value; } }
        public int? ry { get { return _ry; } set { _ry = value; } }
        public int? cx { get { return _cx; } set { _cx = value; } }
        public int? cy { get { return _cy; } set { _cy = value; } }
        public List<ObjectInstanceQuest> QuestInfo { get { return questInfo; } set { questInfo = value; } }

        public new class SerializationForm : LayeredItem.SerializationForm
        {
            public string os, l0, l1, l2;
            public bool flip;
            public MapleBool r;
            public string name;
            public MapleBool hide, reactor, flow;
            public int? rx, ry, cx, cy;
            public string tags;
            public ObjectInstanceQuest[] quest;
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
            result.os = baseInfo.oS;
            result.l0 = baseInfo.l0;
            result.l1 = baseInfo.l1;
            result.l2 = baseInfo.l2;
            result.flip = flip;
            result.r = _r;
            result.name = name;
            result.hide = _hide;
            result.reactor = _reactor;
            result.flow = _flow;
            result.rx = _rx;
            result.ry = _ry;
            result.cx = _cx;
            result.cy = _cy;
            result.tags = tags;
            result.quest = questInfo == null ? null : questInfo.ToArray();
        }

        public ObjectInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            baseInfo = ObjectInfo.Get(json.os, json.l0, json.l1, json.l2);
            flip = json.flip;
            _r = json.r;
            name = json.name;
            _hide = json.hide;
            _reactor = json.reactor;
            _flow = json.flow;
            _rx = json.rx;
            _ry = json.ry;
            _cx = json.cx;
            _cy = json.cy;
            tags = json.tags;
            if (json.quest != null)
                questInfo = json.quest.ToList();
        }
    }
}
