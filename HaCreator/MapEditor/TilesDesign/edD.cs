using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class edD : MapTileDesign
    {
        public edD()
        {
            type = "edD";

            potentials.Add(new MapTileDesignPotential("enH1", -90, 0));
            potentials.Add(new MapTileDesignPotential("enH1", 0, 0));
            potentials.Add(new MapTileDesignPotential("enV0", 0, -60));
            potentials.Add(new MapTileDesignPotential("enV1", 0, -60));
            potentials.Add(new MapTileDesignPotential("edU", 0, 0));
            potentials.Add(new MapTileDesignPotential("slLD", 90, 0));
            potentials.Add(new MapTileDesignPotential("slLD", 0, -60));
            potentials.Add(new MapTileDesignPotential("slRD", -90, 0));
            potentials.Add(new MapTileDesignPotential("slRD", 0, -60));
        }

    }
}
