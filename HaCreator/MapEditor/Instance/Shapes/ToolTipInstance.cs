/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class ToolTipInstance : MapleRectangle, ISerializable // Renamed to ToolTipInstance to avoid ambiguity with System.Windows.Forms.ToolTip
    {
        private string title;
        private string desc;
        private ToolTipChar ttc = null;
        private int originalNum;

        public ToolTipInstance(Board board, XNA.Rectangle rect, string title, string desc, int originalNum = -1)
            : base(board, rect)
        {
            this.title = title;
            this.desc = desc;
            this.originalNum = originalNum;
        }

        public override MapleDot CreateDot(int x, int y)
        {
            return new ToolTipDot(this, board, x, y);
        }

        public override MapleLine CreateLine(MapleDot a, MapleDot b)
        {
            return new ToolTipLine(board, a, b);
        }

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public string Desc
        {
            get { return desc; }
            set { desc = value; }
        }

        public ToolTipChar CharacterToolTip
        {
            get { return ttc; }
            set { ttc = value; }
        }

        public int OriginalNumber
        {
            get { return originalNum; }
        }

        public override XNA.Color Color
        {
            get
            {
                return Selected ? UserSettings.ToolTipSelectedFill : UserSettings.ToolTipFill;
            }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.ToolTips; }
        }

        public override void Draw(SpriteBatch sprite, XNA.Color dotColor, int xShift, int yShift)
        {
            base.Draw(sprite, dotColor, xShift, yShift);
            if (title != null)
            {
                Board.ParentControl.FontEngine.DrawString(sprite, new System.Drawing.Point(X + xShift + 2, Y + yShift + 2), Microsoft.Xna.Framework.Color.Black, title, Width);
            }
            if (desc != null)
            {
                int titleHeight = (int)Math.Ceiling(Board.ParentControl.FontEngine.MeasureString(title).Height);
                Board.ParentControl.FontEngine.DrawString(sprite, new System.Drawing.Point(X + xShift + 2, Y + yShift + 2 + titleHeight), Microsoft.Xna.Framework.Color.Black, desc, Width);
            }
        }

        public override void RemoveItem(List<UndoRedoAction> undoPipe)
        {
            lock (board.ParentControl)
            {
                base.RemoveItem(undoPipe);
                if (ttc != null)
                    ttc.RemoveItem(undoPipe);
            }
        }

        public void CreateCharacterTooltip(XNA.Rectangle rect)
        {
            lock (board.ParentControl)
            {
                ttc = new ToolTipChar(Board, rect, this);
                List<UndoRedoAction> undoPipe = new List<UndoRedoAction>();
                ttc.OnItemPlaced(undoPipe);
                Board.BoardItems.CharacterToolTips.Add(ttc);
                Board.UndoRedoMan.AddUndoBatch(undoPipe);
            }
        }

        public new class SerializationForm : MapleRectangle.SerializationForm
        {
            public string title, desc;
            public int originalnum;
        }

        public override bool ShouldSelectSerialized
        {
            get
            {
                return base.ShouldSelectSerialized || ttc != null;
            }
        }

        public override List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems)
        {
            List<ISerializableSelector> result = base.SelectSerialized(serializedItems);
            if (ttc != null)
                result.Add(ttc);
            return result;
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
            result.title = title;
            result.desc = desc;
            result.originalnum = originalNum;
        }

        private const string TTC_KEY = "ttc";

        public override IDictionary<string, object> SerializeBindings(Dictionary<ISerializable, long> refDict)
        {
            if (ttc != null)
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result[TTC_KEY] = refDict[ttc];
                return result;
            }
            else
            {
                return null;
            }
        }

        public override void DeserializeBindings(IDictionary<string, object> bindSer, Dictionary<long, ISerializable> refDict)
        {
            if (bindSer != null)
            {
                ttc = (ToolTipChar)refDict[(long)bindSer[TTC_KEY]];
                ttc.BoundTooltip = this;
            }
        }

        public ToolTipInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            title = json.title;
            desc = json.desc;
            originalNum = json.originalnum;
        }
    }
}
