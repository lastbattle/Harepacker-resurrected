using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.ClientLib
{
    /// <summary>
    /// The localisation number for each regional MapleStory version.
    /// </summary>
    public enum MapleStoryLocalisation : int
    {
        MapleStoryKorea = 1, 
        MapleStoryKoreaTespia = 2,
        MapleStoryTespia = 5,
        MapleStorySEA = 7,
        MapleStoryGlobal = 8,
        MapleStoryEurope = 9,

        Not_Known = 999,

        // TODO: other values
    }
}
