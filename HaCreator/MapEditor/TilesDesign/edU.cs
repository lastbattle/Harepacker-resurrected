using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class edU : MapTileDesign
    {
        public edU()
        {
            type = "edU";

            potentials.Add(new MapTileDesignPotential("enH0", -90, 0));
            potentials.Add(new MapTileDesignPotential("enH0", 0, 0));
            potentials.Add(new MapTileDesignPotential("enV0", 0, 0));
            potentials.Add(new MapTileDesignPotential("enV1", 0, 0));
            potentials.Add(new MapTileDesignPotential("edD", 0, 0));
            potentials.Add(new MapTileDesignPotential("slLU", 90, 0));
            potentials.Add(new MapTileDesignPotential("slLU", 0, 60));
            potentials.Add(new MapTileDesignPotential("slRU", -90, 0));
            potentials.Add(new MapTileDesignPotential("slRU", 0, 60));
        }

    }
}
