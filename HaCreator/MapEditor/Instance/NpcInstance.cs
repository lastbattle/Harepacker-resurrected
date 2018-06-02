/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Instance
{
    public class NpcInstance : LifeInstance
    {
        private NpcInfo baseInfo;

        public NpcInstance(NpcInfo baseInfo, Board board, int x, int y, int rx0Shift, int rx1Shift, int yShift, string limitedname, int? mobTime, MapleBool flip, MapleBool hide, int? info, int? team)
            : base(baseInfo, board, x, y, rx0Shift, rx1Shift, yShift, limitedname, mobTime, flip, hide, info, team) 
        {
            this.baseInfo = baseInfo;
        }

        public override MapleDrawableInfo BaseInfo
        {
            get { return baseInfo; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.NPCs; }
        }

        public new class SerializationForm : LifeInstance.SerializationForm
        {
            public string id;
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
            result.id = baseInfo.ID;
        }

        public NpcInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            baseInfo = NpcInfo.Get(json.id);
        }
    }
}
