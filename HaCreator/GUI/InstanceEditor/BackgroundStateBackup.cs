using HaCreator.MapEditor.Instance;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.InstanceEditor
{
    public class BackgroundStateBackup
    {
        public int BaseX { get; set; }
        public int BaseY { get; set; }
        public int Z { get; set; }
        public bool Front { get; set; }
        public BackgroundType Type { get; set; }
        public int A { get; set; }
        public int Rx { get; set; }
        public int Ry { get; set; }
        public int Cx { get; set; }
        public int Cy { get; set; }
        public int ScreenMode { get; set; }
        public string SpineAni { get; set; }
        public bool SpineRandomStart { get; set; }

        public BackgroundStateBackup(BackgroundInstance item)
        {
            this.BaseX = item.BaseX;
            this.BaseY = item.BaseY;
            this.Z = item.Z;
            this.Front = item.front;
            this.Type = item.type;
            this.A = item.a;
            this.Rx = item.rx;
            this.Ry = item.ry;
            this.Cx = item.cx;
            this.Cy = item.cy;
            this.ScreenMode = item.screenMode;
            this.SpineAni = item.SpineAni;
            this.SpineRandomStart = item.SpineRandomStart;
        }
    }
}
