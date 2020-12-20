using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data
{
    public enum BackgroundType
    {
        Regular = 0,
        HorizontalTiling = 1, // Horizontal copy
        VerticalTiling = 2, // Vertical copy
        HVTiling = 3,
        HorizontalMoving = 4,
        VerticalMoving = 5,
        HorizontalMovingHVTiling = 6,
        VerticalMovingHVTiling = 7
    }
}
