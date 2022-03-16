/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Misc
{
    public class BuffZone : MiscRectangle, ISerializable
    {
        private int itemID;
        private int interval;
        private int duration;
        private string zoneName;

        public BuffZone(Board board, XNA.Rectangle rect, int itemID, int interval, int duration, string zoneName)
            : base(board, rect)
        {
            this.itemID = itemID;
            this.interval = interval;
            this.duration = duration;
            this.zoneName = zoneName;
        }

        public override string Name
        {
            get { return "BuffZone " + this.zoneName; }
        }

        public int ItemID
        {
            get { return itemID; }
            set { itemID = value; }
        }

        public int Interval
        {
            get { return interval; }
            set { interval = value; }
        }

        public int Duration
        {
            get { return duration; }
            set { duration = value; }
        }
        public string ZoneName
        {
            get { return zoneName; }
            set { zoneName = value; }
        }

        public new class SerializationForm : MapleRectangle.SerializationForm
        {
            public int itemid, interval, duration;
            public string zonename;
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
            result.itemid = itemID;
            result.interval = interval;
            result.duration = duration;
            result.zonename = zoneName;
        }

        public BuffZone(Board board, SerializationForm json)
            : base(board, json)
        {
            itemID = json.itemid;
            interval = json.interval;
            duration = json.duration;
            zoneName = json.zonename;
        }
    }
}
