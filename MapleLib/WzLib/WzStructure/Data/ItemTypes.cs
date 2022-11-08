using System;

namespace MapleLib.WzLib.WzStructure.Data
{
    [Flags]
    public enum ItemTypes
    {
        None = 0x0,

        Tiles = 0x1,
        Objects = 0x2,
        Mobs = 0x4,
        NPCs = 0x8,
        Ropes = 0x10,
        Footholds = 0x20,
        Portals = 0x40,
        Chairs = 0x80,
        Reactors = 0x100,
        ToolTips = 0x200,
        Backgrounds = 0x400,
        Misc = 0x800,
        MirrorFieldData = 0x1000,

        All = 0xFFF
    }
}
