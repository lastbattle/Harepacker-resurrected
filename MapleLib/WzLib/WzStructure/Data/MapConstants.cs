using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data
{
    public class MapConstants
    {
        public const int MinMap = 0;
        public const int MaxMap = 999999999;

        public const int MaxMapLayers = 8;

        /// <summary>
        /// MapId for Zero's temple
        /// </summary>
        /// <param name="MapId"></param>
        /// <returns></returns>
        public static bool IsZerosTemple(int MapId)
        {
            return MapId / 10000000 == 32;
        }
    }
}
