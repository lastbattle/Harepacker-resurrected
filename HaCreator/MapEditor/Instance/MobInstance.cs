/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapEditor.Instance
{
    public class MobInstance : LifeInstance
    {
        private MobInfo baseInfo;
        public MobInfo MobInfo { get { return baseInfo; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseInfo"></param>
        /// <param name="board"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="rx0Shift"></param>
        /// <param name="rx1Shift"></param>
        /// <param name="yShift"></param>
        /// <param name="limitedname"></param>
        /// <param name="mobTime"></param>
        /// <param name="flip"></param>
        /// <param name="hide"></param>
        /// <param name="info"></param>
        /// <param name="team"></param>
        public MobInstance(MobInfo baseInfo, Board board, int x, int y, int rx0Shift, int rx1Shift, int yShift, string limitedname, int? mobTime, MapleBool flip, MapleBool hide, int? info, int? team)
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
            get { return ItemTypes.Mobs; }
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

        public MobInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            baseInfo = MobInfo.Get(json.id);
        }
    }
}
