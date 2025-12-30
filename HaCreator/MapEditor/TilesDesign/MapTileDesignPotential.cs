using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.TilesDesign
{
    public class MapTileDesignPotential
    {
        public string type;
        public int x, y;

        public MapTileDesignPotential(string type, int x, int y)
        {
            this.type = type;
            this.x = x;
            this.y = y;
        }
    }
}
