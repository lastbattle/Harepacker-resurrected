using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class enV1 : MapTileDesign
    {
        public enV1()
        {
            type = "enV1";

            potentials.Add(new MapTileDesignPotential("bsc", -90, 0));
            potentials.Add(new MapTileDesignPotential("enH0", 0, 60));
            potentials.Add(new MapTileDesignPotential("enH1", 0, 0));
            potentials.Add(new MapTileDesignPotential("enV0", 0, 0));
            potentials.Add(new MapTileDesignPotential("enV1", 0, -60));
            potentials.Add(new MapTileDesignPotential("enV1", 0, 60));
            potentials.Add(new MapTileDesignPotential("edU", 0, 0));
            potentials.Add(new MapTileDesignPotential("edD", 0, 60));
            potentials.Add(new MapTileDesignPotential("slLU", 0, 60));
            potentials.Add(new MapTileDesignPotential("slLD", 0, 0));
        }

    }
}
