/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class FootholdLine : MapleLine, IContainsLayerInfo, ISerializable
    {
        private MapleBool _cantThrough;
        private MapleBool _forbidFallDown;
        private int? _piece;
        private int? _force;

        // internal use variables
        public int prev = 0;
        public int next = 0;
        public FootholdLine prevOverride = null;
        public FootholdLine nextOverride = null;
        public int num;
        public bool saved;

        public FootholdLine(Board board, MapleDot firstDot, MapleDot secondDot)
            : base(board, firstDot, secondDot)
        {
            this._cantThrough = null;
            this._forbidFallDown = null;
            this._piece = null;
            this._force = null;
        }

        public FootholdLine(Board board, MapleDot firstDot)
            : base(board, firstDot)
        {
            this._cantThrough = null;
            this._forbidFallDown = null;
            this._piece = null;
            this._force = null;
        }

        public FootholdLine(Board board, MapleDot firstDot, MapleDot secondDot, MapleBool forbidFallDown, MapleBool cantThrough, int? piece, int? force)
            : base(board, firstDot, secondDot)
        {
            this._cantThrough = cantThrough;
            this._forbidFallDown = forbidFallDown;
            this._piece = piece;
            this._force = force;
        }

        public FootholdLine(Board board, MapleDot firstDot, MapleBool forbidFallDown, MapleBool cantThrough, int? piece, int? force)
            : base(board, firstDot)
        {
            this._cantThrough = cantThrough;
            this._forbidFallDown = forbidFallDown;
            this._piece = piece;
            this._force = force;
        }

        public override XNA.Color Color
        {
            get { return UserSettings.FootholdColor; }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.FootholdInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Footholds; }
        }

        public bool FhEquals(FootholdLine obj)
        {
            return ((((FootholdLine)obj).FirstDot.X == FirstDot.X && ((FootholdLine)obj).SecondDot.X == SecondDot.X)
                && (((FootholdLine)obj).FirstDot.Y == FirstDot.Y && ((FootholdLine)obj).SecondDot.Y == SecondDot.Y))
                || ((((FootholdLine)obj).FirstDot.X == SecondDot.X && ((FootholdLine)obj).SecondDot.X == FirstDot.X)
                && (((FootholdLine)obj).FirstDot.Y == SecondDot.Y && ((FootholdLine)obj).SecondDot.Y == FirstDot.Y));
        }

        public static bool Exists(int x1, int y1, int x2, int y2, Board board)
        {
            foreach (FootholdLine fh in board.BoardItems.FootholdLines)
            {
                if (((fh.FirstDot.X == x1 && fh.FirstDot.Y == y1) &&
                    (fh.SecondDot.X == x2 && fh.SecondDot.Y == y2)) ||
                    ((fh.FirstDot.X == x2 && fh.FirstDot.Y == y2) &&
                    (fh.SecondDot.X == x1 && fh.SecondDot.Y == y1))) return true;
            }
            return false;
        }

        public static FootholdLine[] GetSelectedFootholds(Board board)
        {
            int length = 0;
            foreach (FootholdLine line in board.BoardItems.FootholdLines)
                if (line.Selected) length++;
            FootholdLine[] result = new FootholdLine[length];
            int index = 0;
            foreach (FootholdLine line in board.BoardItems.FootholdLines)
                if (line.Selected) { result[index] = line; index++; }
            return result;
        }

        public bool IsWall { get { return FirstDot.X == SecondDot.X; } }

        public int? Force { get { return _force; } set { _force = value; } }
        public int? Piece { get { return _piece; } set { _piece = value; } }
        public MapleBool ForbidFallDown { get { return _forbidFallDown; } set { _forbidFallDown = value; } }
        public MapleBool CantThrough { get { return _cantThrough; } set { _cantThrough = value; } }
        public int LayerNumber { get { return ((FootholdAnchor)FirstDot).LayerNumber; } set { throw new NotImplementedException(); } }
        public int PlatformNumber { get { return ((FootholdAnchor)FirstDot).PlatformNumber; } set { throw new NotImplementedException(); } }

        public static int FHSorter(FootholdLine a, FootholdLine b)
        {
            if (a.FirstDot.X > b.FirstDot.X)
                return 1;
            else if (a.FirstDot.X < b.FirstDot.X)
                return -1;
            else
            {
                if (a.FirstDot.Y > b.FirstDot.Y)
                    return 1;
                else if (a.FirstDot.Y < b.FirstDot.Y)
                    return -1;
                else
                {
                    if (a.SecondDot.X > b.SecondDot.X)
                        return 1;
                    else if (a.SecondDot.X < b.SecondDot.X)
                        return -1;
                    else
                    {
                        if (a.SecondDot.Y > b.SecondDot.Y)
                            return 1;
                        else if (a.SecondDot.Y < b.SecondDot.Y)
                            return -1;
                        else
                            return 0;
                    }
                }
            }
        }

        public FootholdAnchor GetOtherAnchor(FootholdAnchor first)
        {
            if (FirstDot == first)
                return (FootholdAnchor)SecondDot;
            else if (SecondDot == first)
                return (FootholdAnchor)FirstDot;
            else
                throw new Exception("GetOtherAnchor: line is not properly connected");
        }

        #region ISerializable Implementation
        public class SerializationForm
        {
            public MapleBool cantthrough, forbidfalldown;
            public int? piece, force;
        }

        public bool ShouldSelectSerialized
        {
            get
            {
                return true;
            }
        }

        public List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems)
        {
            List<ISerializableSelector> result = new List<ISerializableSelector>();
            // We add the dots to make sure they are serialized (we might have been added as a prev/next override of another line)
            result.Add(FirstDot);
            result.Add(SecondDot);
            if (prevOverride != null)
                result.Add(prevOverride);
            if (nextOverride != null)
                result.Add(nextOverride);
            return result;
        }

        public object Serialize()
        {
            SerializationForm result = new SerializationForm();
            result.cantthrough = _cantThrough;
            result.forbidfalldown = _forbidFallDown;
            result.piece = _piece;
            result.force = _force;
            return result;
        }

        private const string FIRSTDOT_KEY = "dot1";
        private const string SECONDDOT_KEY = "dot2";
        private const string PREVOVERRIDE_KEY = "prevOverride";
        private const string NEXTOVERRIDE_KEY = "nextOverride";

        public IDictionary<string, object> SerializeBindings(Dictionary<ISerializable, long> refDict)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result[FIRSTDOT_KEY] = refDict[(FootholdAnchor)FirstDot];
            result[SECONDDOT_KEY] = refDict[(FootholdAnchor)SecondDot];
            if (prevOverride != null)
                result[PREVOVERRIDE_KEY] = refDict[prevOverride];
            if (nextOverride != null)
                result[NEXTOVERRIDE_KEY] = refDict[nextOverride];
            return result;
        }

        public FootholdLine(Board board, SerializationForm json)
            : base(board)
        {
            _cantThrough = json.cantthrough;
            _forbidFallDown = json.forbidfalldown;
            _piece = json.piece;
            _force = json.force;
        }

        public void DeserializeBindings(IDictionary<string, object> bindSer, Dictionary<long, ISerializable> refDict)
        {
            firstDot = (FootholdAnchor)refDict[(long)bindSer[FIRSTDOT_KEY]];
            secondDot = (FootholdAnchor)refDict[(long)bindSer[SECONDDOT_KEY]];
            if (bindSer.ContainsKey(PREVOVERRIDE_KEY))
                prevOverride = (FootholdLine)refDict[(long)bindSer[PREVOVERRIDE_KEY]];
            if (bindSer.ContainsKey(NEXTOVERRIDE_KEY))
                prevOverride = (FootholdLine)refDict[(long)bindSer[NEXTOVERRIDE_KEY]];
            firstDot.connectedLines.Add(this);
            secondDot.connectedLines.Add(this);
            firstDot.PointMoved += OnFirstDotMoved;
            secondDot.PointMoved += OnSecondDotMoved;
        }

        public void AddToBoard(List<UndoRedoAction> undoPipe)
        {
            base.OnPlaced(undoPipe);
            Board.BoardItems.FootholdLines.Add(this);
        }

        public void PostDeserializationActions(bool? selected, XNA.Point? offset)
        {
            // Nothing to do here, we cant be offset nor selected.
        }
        #endregion
    }
}
