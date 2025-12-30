using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.TilesDesign
{
    public abstract class MapTileDesign
    {
        public string type;

        public List<MapTileDesignPotential> potentials = new List<MapTileDesignPotential>();

        public MapTileDesign()
        {
        }
    }
}
